using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore :
    IFlashcardDeckReader,
    IFlashcardDashboardReader,
    IFlashcardReviewReader,
    IFlashcardNoteGenerationReader,
    IFlashcardAuthoringWriter,
    IFlashcardReviewWriter,
    IFlashcardCollectionImportWriter
{
    private readonly IFlashcardScheduler scheduler;
    private readonly ILogger<SqliteFlashcardStore>? logger;

    internal SqliteFlashcardStore(
        IFlashcardScheduler scheduler,
        ILogger<SqliteFlashcardStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        this.scheduler = scheduler;
        this.logger = logger;
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
        IReadOnlyList<string>? tags,
        string? searchText,
        int limit)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath, logger);

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
        CardSummaries loadedSummaries = CardSummaryMaterializer.Load(collectionPath, cards, tag, tags, searchText);

        repository.MarkCardsDeleted(loadedSummaries.StaleCardIds);
        if (loadedSummaries.StaleCardIds.Count > 0)
        {
            logger?.LogWarning(
                "Marked stale flashcards deleted while loading card summaries because content files are missing. CollectionPath={CollectionPath} StaleCardCount={StaleCardCount}",
                collectionPath,
                loadedSummaries.StaleCardIds.Count);
        }

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
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionId(collectionId, logger);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        IReadOnlyList<FlashcardDeckSummaryReadModel> summaries = repository
            .LoadDeckSummaries(nowUtc)
            .Select(ToDeckSummaryReadModel)
            .ToArray();
        return summaries;
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
