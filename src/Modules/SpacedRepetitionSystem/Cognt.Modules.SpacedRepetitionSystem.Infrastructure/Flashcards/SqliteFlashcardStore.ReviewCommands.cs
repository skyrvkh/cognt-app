using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using PersistenceRating = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewRatingReadModel;
using PersistenceSettings = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewSettingsReadModel;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    public Task UpdateReviewSettingsAsync(
        string collectionId,
        AppReadModels.FlashcardReviewSettingsReadModel settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        cancellationToken.ThrowIfCancellationRequested();

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId, logger);
        repository.SaveReviewSettings(ToPersistenceSettings(settings));
        return Task.CompletedTask;
    }

    public async Task<AppReadModels.RecordedFlashcardReviewReadModel> RecordReviewAsync(
        string collectionId,
        long cardId,
        AppReadModels.FlashcardReviewRatingReadModel rating,
        int? reviewDurationMilliseconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        FlashcardRecord card = repository.GetCard(cardId);
        PersistenceSettings settings = repository.LoadReviewSettings();
        (double desiredRetention, IReadOnlyList<double>? parameters) = CreateSchedulerOptimization(repository, settings);
        FlashcardSchedule schedule = await scheduler.ReviewCardAsync(
            card.SerializedState,
            rating,
            desiredRetention,
            settings.LearningSteps,
            settings.RelearningSteps,
            parameters,
            reviewDurationMilliseconds,
            cancellationToken);

        repository.ApplyReview(cardId, ToPersistenceRating(rating), schedule);
        AppReadModels.RecordedFlashcardReviewReadModel recordedReview = new(
            cardId,
            schedule.DueUtc,
            ToCardStateReadModel(schedule.State),
            schedule.Step);
        return recordedReview;
    }

    public Task SetCardSuspensionAsync(
        string collectionId,
        long cardId,
        bool isSuspended,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId, logger);
        repository.SetCardSuspension(cardId, isSuspended);
        return Task.CompletedTask;
    }

    public Task PostponeCardDueDateAsync(
        string collectionId,
        long cardId,
        int days,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        FlashcardRecord card = repository.GetCard(cardId);
        StoredFsrsCard stored = FsrsStoredStateCodec.Read(card.SerializedState);
        FsrsCard postponedCard = stored.Card.Copy();
        DateTimeOffset dueUtc = card.DueUtc.AddDays(days);
        postponedCard.Due = dueUtc;

        StoredFsrsCard postponedStored = new(
            postponedCard,
            stored.Reps,
            stored.Lapses,
            stored.ElapsedDays,
            Math.Max(0, stored.ScheduledDays + days));

        repository.PostponeCardDueDate(
            cardId,
            dueUtc,
            FsrsStoredStateCodec.Serialize(postponedStored),
            postponedStored.ScheduledDays);
        return Task.CompletedTask;
    }

    private (double DesiredRetention, IReadOnlyList<double>? Parameters) CreateSchedulerOptimization(
        SqliteFlashcardRepository repository,
        PersistenceSettings settings)
    {
        if (!settings.IsOptimizationEnabled)
        {
            return (settings.DesiredRetention, settings.FsrsParameters);
        }

        IReadOnlyList<FsrsReviewLog> reviewLogs = repository.LoadFsrsReviewLogs();
        if (reviewLogs.Count < 512)
        {
            return (settings.DesiredRetention, settings.FsrsParameters);
        }

        FsrsOptimizer optimizer = new(reviewLogs);
        double[] parameters = optimizer.ComputeOptimalParameters();
        if (reviewLogs.Any(log => log.ReviewDuration is null))
        {
            return (settings.DesiredRetention, parameters);
        }

        return (optimizer.ComputeOptimalRetention(parameters), parameters);
    }

    private static PersistenceSettings ToPersistenceSettings(
        AppReadModels.FlashcardReviewSettingsReadModel settings)
    {
        return new PersistenceSettings(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention,
            settings.LearningSteps,
            settings.RelearningSteps,
            settings.IsOptimizationEnabled,
            settings.FsrsParameters,
            settings.IsCramEnabled,
            settings.ShowFlashcardsInNavigationTree).Normalize();
    }

    private static PersistenceRating ToPersistenceRating(
        AppReadModels.FlashcardReviewRatingReadModel rating)
    {
        return (PersistenceRating)(int)rating;
    }

    private static AppReadModels.FlashcardCardStateReadModel ToCardStateReadModel(
        FlashcardLearningState state)
    {
        return state switch
        {
            FlashcardLearningState.New => AppReadModels.FlashcardCardStateReadModel.New,
            FlashcardLearningState.Learning => AppReadModels.FlashcardCardStateReadModel.Learning,
            FlashcardLearningState.Review => AppReadModels.FlashcardCardStateReadModel.Review,
            FlashcardLearningState.Relearning => AppReadModels.FlashcardCardStateReadModel.Relearning,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported flashcard state."),
        };
    }
}
