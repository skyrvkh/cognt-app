using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using AppQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCandidate;
using PersistenceQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCandidate;
using PersistenceQueueCategory = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCategory;
using PersistenceQueueCounts = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCounts;
using PersistenceReviewLimitUsage = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewLimitUsage;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    internal AppReadModels.FlashcardReviewSettingsReadModel LoadReviewSettings(string collectionId)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId, logger);
        return ToReadModel(repository.LoadReviewSettings());
    }

    internal AppReadModels.FlashcardReviewQueueReadModel LoadReviewQueue(
        string collectionId,
        AppReadModels.FlashcardReviewQueueMode mode = AppReadModels.FlashcardReviewQueueMode.Scheduled,
        CramScope? cramScope = null)
    {
        return mode == AppReadModels.FlashcardReviewQueueMode.Cram
            ? LoadCramReviewQueue(collectionId, cramScope)
            : LoadScheduledReviewQueue(collectionId);
    }

    private AppReadModels.FlashcardReviewQueueReadModel LoadScheduledReviewQueue(string collectionId)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath, logger);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        AppReadModels.FlashcardReviewSettingsReadModel settings = ToReadModel(repository.LoadReviewSettings());
        var (todayStartUtc, tomorrowStartUtc) = GetLocalTodayUtcBounds(nowUtc);
        PersistenceQueueCounts counts = repository.LoadReviewQueueCounts(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc);
        PersistenceReviewLimitUsage usage = repository.LoadReviewLimitUsage(todayStartUtc, tomorrowStartUtc);

        IReadOnlyList<PersistenceQueueCandidate> candidates = repository.LoadReviewQueueCandidates(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc,
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards + counts.DueCards,
            counts.NewCards);
        AppQueueCandidate[] queueCandidates = CreateReviewQueueCandidates(candidates);
        FlashcardReviewQueuePlan plan = FlashcardReviewQueuePlanner.CreatePlan(
            queueCandidates,
            ToReadModel(counts),
            settings,
            ToReadModel(usage));
        DateTimeOffset? nextDueUtc = repository.LoadNextFutureDueUtc(nowUtc);
        DateTimeOffset? nextIntradayDueUtc = repository.LoadNextFutureIntradayDueUtc(nowUtc);
        Dictionary<long, FlashcardRecord> cardsById = CreateCardsById(repository.LoadCardsByIds(plan.CardIds));

        ReviewQueueCards loadedCards = ReviewQueueCardLoader.LoadForPlan(
            collectionPath,
            plan.CardIds,
            cardsById,
            CreateQueuePlacements(queueCandidates),
            logger);
        repository.MarkCardsDeleted(loadedCards.StaleCardIds);
        LogStaleReviewCardsDeleted(collectionPath, loadedCards.StaleCardIds, "Scheduled");
        AppReadModels.FlashcardReviewQueueReadModel queue = new(
            loadedCards.ReviewCards,
            plan.Summary with { TotalCards = loadedCards.ReviewCards.Count },
            nextDueUtc,
            nextIntradayDueUtc);
        return queue;
    }

    private AppReadModels.FlashcardReviewQueueReadModel LoadCramReviewQueue(
        string collectionId,
        CramScope? cramScope)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath, logger);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        var (todayStartUtc, tomorrowStartUtc) = GetLocalTodayUtcBounds(nowUtc);
        CramScope scope = (cramScope ?? CramScope.All).Normalize();
        PersistenceQueueCounts counts = repository.LoadCramReviewQueueCounts(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc,
            scope);
        IReadOnlyList<PersistenceQueueCandidate> candidates = repository.LoadCramReviewQueueCandidates(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc,
            scope);
        HashSet<long> schedulerEligibleCardIds = CreateSchedulerEligibleCardIds(candidates, nowUtc);
        long[] cardIds = CreateCardIds(candidates);
        Dictionary<long, FlashcardRecord> cardsById = CreateCardsById(repository.LoadCardsByIds(cardIds));

        ReviewQueueCards loadedCards = ReviewQueueCardLoader.LoadForPlan(collectionPath, cardIds, cardsById, logger: logger);
        repository.MarkCardsDeleted(loadedCards.StaleCardIds);
        LogStaleReviewCardsDeleted(collectionPath, loadedCards.StaleCardIds, "Cram");
        AppReadModels.FlashcardReviewCardReadModel[] reviewCards =
            ApplyQueueStartDueFlags(loadedCards.ReviewCards, schedulerEligibleCardIds);

        AppReadModels.FlashcardReviewQueueReadModel queue = new(
            reviewCards,
            CreateCramSummary(
                ToReadModel(counts),
                reviewCards.Length,
                cardIds.Length,
                Math.Max(0, cardIds.Length - reviewCards.Length)))
        {
            Mode = AppReadModels.FlashcardReviewQueueMode.Cram,
        };
        return queue;
    }

    private void LogStaleReviewCardsDeleted(
        string collectionPath,
        IReadOnlyCollection<long> staleCardIds,
        string queueMode)
    {
        if (staleCardIds.Count == 0)
        {
            return;
        }

        logger?.LogWarning(
            "Marked stale flashcards deleted while loading review queue because content files are missing. CollectionPath={CollectionPath} QueueMode={QueueMode} StaleCardCount={StaleCardCount}",
            collectionPath,
            queueMode,
            staleCardIds.Count);
    }

    private static Dictionary<long, AppReadModels.FlashcardReviewCardQueuePlacement> CreateQueuePlacements(
        IReadOnlyList<AppQueueCandidate> candidates)
    {
        Dictionary<long, AppReadModels.FlashcardReviewCardQueuePlacement> placements = new(candidates.Count);
        for (int index = 0; index < candidates.Count; index++)
        {
            AppQueueCandidate candidate = candidates[index];
            placements[candidate.CardId] = new AppReadModels.FlashcardReviewCardQueuePlacement(
                candidate.Category,
                candidate.DueUtc);
        }

        return placements;
    }

    private static HashSet<long> CreateSchedulerEligibleCardIds(
        IReadOnlyList<PersistenceQueueCandidate> candidates,
        DateTimeOffset nowUtc)
    {
        HashSet<long> cardIds = [];
        for (int index = 0; index < candidates.Count; index++)
        {
            PersistenceQueueCandidate candidate = candidates[index];
            if (candidate.Category != PersistenceQueueCategory.New
                && candidate.DueUtc <= nowUtc)
            {
                cardIds.Add(candidate.CardId);
            }
        }

        return cardIds;
    }

    private static long[] CreateCardIds(IReadOnlyList<PersistenceQueueCandidate> candidates)
    {
        long[] cardIds = new long[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            cardIds[index] = candidates[index].CardId;
        }

        return cardIds;
    }

    private static long[] CreateCardIds(IReadOnlyList<FlashcardRecord> cards)
    {
        long[] cardIds = new long[cards.Count];
        for (int index = 0; index < cards.Count; index++)
        {
            cardIds[index] = cards[index].Id;
        }

        return cardIds;
    }

    private static Dictionary<long, FlashcardRecord> CreateCardsById(IReadOnlyList<FlashcardRecord> cards)
    {
        Dictionary<long, FlashcardRecord> cardsById = new(cards.Count);
        for (int index = 0; index < cards.Count; index++)
        {
            FlashcardRecord card = cards[index];
            cardsById[card.Id] = card;
        }

        return cardsById;
    }

    private static AppReadModels.FlashcardReviewCardReadModel[] ApplyQueueStartDueFlags(
        IReadOnlyList<AppReadModels.FlashcardReviewCardReadModel> cards,
        HashSet<long> schedulerEligibleCardIds)
    {
        AppReadModels.FlashcardReviewCardReadModel[] reviewCards = new AppReadModels.FlashcardReviewCardReadModel[cards.Count];
        for (int index = 0; index < cards.Count; index++)
        {
            AppReadModels.FlashcardReviewCardReadModel card = cards[index];
            reviewCards[index] = card with
            {
                WasDueAtQueueStart = schedulerEligibleCardIds.Contains(card.Id),
            };
        }

        return reviewCards;
    }

}
