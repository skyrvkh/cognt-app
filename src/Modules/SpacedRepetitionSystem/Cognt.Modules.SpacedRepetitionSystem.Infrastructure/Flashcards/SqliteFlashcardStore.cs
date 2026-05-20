using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore :
    IFlashcardDeckReader,
    IFlashcardDashboardReader,
    IFlashcardReviewReader,
    IFlashcardNoteGenerationReader,
    IFlashcardAuthoringWriter,
    IFlashcardReviewWriter
{
    private readonly IFlashcardScheduler scheduler;

    internal SqliteFlashcardStore(IFlashcardScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        this.scheduler = scheduler;
    }

    IReadOnlyList<FlashcardDeckSummaryReadModel> IFlashcardDeckReader.LoadDeckSummaries(string collectionId)
    {
        return LoadDeckSummaries(collectionId);
    }

    IReadOnlyList<FlashcardCardSummaryReadModel> IFlashcardDeckReader.LoadCardSummaries(
        string collectionId,
        long? deckId,
        FlashcardCardsFilterKind filter,
        string? sourceRelPath,
        string? tag,
        int limit)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        var (todayStartUtc, tomorrowStartUtc) = GetLocalTodayUtcBounds(nowUtc);
        IReadOnlyList<FlashcardCardSummaryRecord> cards = repository.LoadCardSummaries(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc,
            deckId,
            filter,
            sourceRelPath,
            limit);
        CardSummaries loadedSummaries = LoadMaterializedCardSummaries(collectionPath, cards, tag);

        repository.MarkCardsDeleted(loadedSummaries.StaleCardIds);
        return loadedSummaries.Summaries;
    }

    FlashcardDashboardSummaryReadModel IFlashcardDashboardReader.LoadDashboardSummary(
        string collectionId,
        IReadOnlyList<string>? deckNames)
    {
        return LoadDashboardSummary(collectionId, deckNames);
    }

    FlashcardProgressAnalyticsReadModel IFlashcardDashboardReader.LoadProgressAnalytics(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string>? deckNames)
    {
        return LoadProgressAnalytics(collectionId, today, deckNames);
    }

    Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardReviewSettingsReadModel IFlashcardReviewReader.LoadReviewSettings(
        string collectionId)
    {
        return LoadReviewSettings(collectionId);
    }

    FlashcardReviewQueueReadModel IFlashcardReviewReader.LoadReviewQueue(
        string collectionId,
        FlashcardReviewQueueMode mode,
        CramScope? cramScope)
    {
        return LoadReviewQueue(collectionId, mode, cramScope);
    }

    internal IReadOnlyList<FlashcardDeckSummaryReadModel> LoadDeckSummaries(string collectionId)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionId(collectionId);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        return repository
            .LoadDeckSummaries(nowUtc)
            .Select(ToDeckSummaryReadModel)
            .ToArray();
    }

    private static FlashcardDeckSummaryReadModel ToDeckSummaryReadModel(
        FlashcardDeckSummaryRecord summary)
    {
        return new FlashcardDeckSummaryReadModel(
            summary.Id,
            summary.Name,
            summary.NewCards,
            summary.LearningCards,
            summary.DueCards,
            summary.TotalCards);
    }
}
