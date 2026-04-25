using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using PersistenceRating = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewRatingReadModel;
using PersistenceSettings = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewSettingsReadModel;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    internal Task UpdateReviewSettingsAsync(
        string collectionId,
        AppReadModels.FlashcardReviewSettingsReadModel settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        cancellationToken.ThrowIfCancellationRequested();
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId);
        repository.SaveReviewSettings(ToPersistenceSettings(settings));
        return Task.CompletedTask;
    }

    internal Task RecordReviewAsync(
        string collectionId,
        long cardId,
        AppReadModels.FlashcardReviewRatingReadModel rating,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        FlashcardRecord card = repository.GetCard(cardId);
        PersistenceSettings settings = repository.LoadReviewSettings();
        FlashcardSchedule schedule = CreateReviewedSchedule(card, rating, settings.DesiredRetention);

        repository.ApplyReview(cardId, ToPersistenceRating(rating), schedule);
        return Task.CompletedTask;
    }

    private static FlashcardSchedule CreateReviewedSchedule(
        FlashcardRecord card,
        AppReadModels.FlashcardReviewRatingReadModel rating,
        double desiredRetention)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        FlashcardLearningState state = rating == AppReadModels.FlashcardReviewRatingReadModel.Again
            ? FlashcardLearningState.Relearning
            : FlashcardLearningState.Review;
        int scheduledDays = CalculateScheduledDays(card.State, rating, desiredRetention);
        DateTimeOffset dueUtc = scheduledDays == 0
            ? nowUtc.AddMinutes(10)
            : nowUtc.AddDays(scheduledDays);
        int lapses = rating == AppReadModels.FlashcardReviewRatingReadModel.Again ? 1 : 0;
        StoredFsrsCard stored = new(
            new FsrsCard(card.Id, ToFsrsState(state), null, null, null, dueUtc, nowUtc),
            1,
            lapses,
            0,
            scheduledDays);

        return new FlashcardSchedule(
            FsrsStoredStateCodec.Serialize(stored),
            new FsrsReviewLog(card.Id, (FsrsRating)(int)rating, nowUtc).ToJson(),
            dueUtc,
            state,
            null,
            null,
            null,
            nowUtc,
            0,
            scheduledDays,
            1,
            lapses);
    }

    private static int CalculateScheduledDays(
        FlashcardLearningState currentState,
        AppReadModels.FlashcardReviewRatingReadModel rating,
        double desiredRetention)
    {
        double retentionFactor = Math.Clamp(desiredRetention, 0.7d, 0.99d);
        int baseDays = rating switch
        {
            AppReadModels.FlashcardReviewRatingReadModel.Again => 0,
            AppReadModels.FlashcardReviewRatingReadModel.Hard => 1,
            AppReadModels.FlashcardReviewRatingReadModel.Good => currentState == FlashcardLearningState.New ? 1 : 3,
            AppReadModels.FlashcardReviewRatingReadModel.Easy => currentState == FlashcardLearningState.New ? 3 : 7,
            _ => 1,
        };

        return baseDays == 0
            ? 0
            : Math.Max(1, (int)Math.Round(baseDays * retentionFactor));
    }

    private static FsrsState ToFsrsState(FlashcardLearningState state)
    {
        return state switch
        {
            FlashcardLearningState.Relearning => FsrsState.Relearning,
            FlashcardLearningState.Review => FsrsState.Review,
            _ => FsrsState.Learning,
        };
    }

    private static PersistenceSettings ToPersistenceSettings(
        AppReadModels.FlashcardReviewSettingsReadModel settings)
    {
        return new PersistenceSettings(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention).Normalize();
    }

    private static PersistenceRating ToPersistenceRating(
        AppReadModels.FlashcardReviewRatingReadModel rating)
    {
        return (PersistenceRating)(int)rating;
    }
}
