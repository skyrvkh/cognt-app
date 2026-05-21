using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsFlashcardScheduler : IFlashcardScheduler
{
    Task<FlashcardSchedule> IFlashcardScheduler.CreateNewCardAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ToSchedule(
            CreateInitialStoredCard(),
            reviewLog: null,
            FlashcardLearningState.New));
    }

    Task<FlashcardSchedule> IFlashcardScheduler.ReviewCardAsync(
        string serializedState,
        FlashcardReviewRatingReadModel rating,
        double desiredRetention,
        IReadOnlyList<TimeSpan>? learningSteps,
        IReadOnlyList<TimeSpan>? relearningSteps,
        IReadOnlyList<double>? parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StoredFsrsCard previous = FsrsStoredStateCodec.Read(serializedState);
        DateTimeOffset reviewAt = DateTimeOffset.UtcNow;
        int elapsedDays = CalculateElapsedDays(previous.Card.LastReview, reviewAt);

        FsrsScheduler scheduler = CreateScheduler(desiredRetention, learningSteps, relearningSteps, parameters);
        (FsrsCard nextCard, FsrsReviewLog reviewLog) = scheduler.ReviewCard(
            previous.Card,
            ToFsrsRating(rating),
            reviewAt);

        StoredFsrsCard next = CreateReviewedStoredCard(previous, nextCard, rating, elapsedDays, reviewAt);

        return Task.FromResult(ToSchedule(
            next,
            reviewLog.ToJson(),
            ToLearningState(nextCard.State)));
    }

    private static FsrsScheduler CreateScheduler(
        double desiredRetention,
        IReadOnlyList<TimeSpan>? learningSteps,
        IReadOnlyList<TimeSpan>? relearningSteps,
        IReadOnlyList<double>? parameters)
    {
        return new FsrsScheduler(
            parameters,
            desiredRetention: Math.Clamp(
                desiredRetention,
                FlashcardReviewSettingsReadModel.MinimumDesiredRetention,
                FlashcardReviewSettingsReadModel.MaximumDesiredRetention),
            learningSteps: learningSteps,
            relearningSteps: relearningSteps);
    }

}
