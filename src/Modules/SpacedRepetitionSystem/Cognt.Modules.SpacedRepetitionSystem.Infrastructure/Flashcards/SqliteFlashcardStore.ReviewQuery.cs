using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using AppQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCandidate;
using AppQueueCategory = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCategory;
using AppQueueCounts = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCounts;
using AppReviewLimitUsage = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewLimitUsage;
using PersistenceQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCandidate;
using PersistenceQueueCategory = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCategory;
using PersistenceQueueCounts = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCounts;
using PersistenceReviewLimitUsage = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewLimitUsage;
using PersistenceSettings = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewSettingsReadModel;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    internal AppReadModels.FlashcardReviewSettingsReadModel LoadReviewSettings(string collectionId)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId);
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
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath);

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
        FlashcardReviewQueuePlan plan = FlashcardReviewQueuePlanner.CreatePlan(
            candidates.Select(ToReadModel).ToArray(),
            ToReadModel(counts),
            settings,
            ToReadModel(usage));
        DateTimeOffset? nextDueUtc = repository.LoadNextFutureDueUtc(nowUtc);
        DateTimeOffset? nextIntradayDueUtc = repository.LoadNextFutureIntradayDueUtc(nowUtc);
        Dictionary<long, FlashcardRecord> cardsById = repository
            .LoadCardsByIds(plan.CardIds)
            .ToDictionary(card => card.Id);

        ReviewQueueCards loadedCards = LoadReviewCardsForPlan(collectionPath, plan.CardIds, cardsById);
        repository.MarkCardsDeleted(loadedCards.StaleCardIds);
        return new AppReadModels.FlashcardReviewQueueReadModel(
            loadedCards.ReviewCards,
            plan.Summary with { TotalCards = loadedCards.ReviewCards.Count },
            nextDueUtc,
            nextIntradayDueUtc);
    }

    private AppReadModels.FlashcardReviewQueueReadModel LoadCramReviewQueue(
        string collectionId,
        CramScope? cramScope)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath);

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
        HashSet<long> schedulerEligibleCardIds = candidates
            .Where(candidate => candidate.Category != PersistenceQueueCategory.New
                && candidate.DueUtc <= nowUtc)
            .Select(candidate => candidate.CardId)
            .ToHashSet();
        long[] cardIds = candidates
            .Select(candidate => candidate.CardId)
            .ToArray();
        Dictionary<long, FlashcardRecord> cardsById = repository
            .LoadCardsByIds(cardIds)
            .ToDictionary(card => card.Id);

        ReviewQueueCards loadedCards = LoadReviewCardsForPlan(collectionPath, cardIds, cardsById);
        repository.MarkCardsDeleted(loadedCards.StaleCardIds);
        AppReadModels.FlashcardReviewCardReadModel[] reviewCards = loadedCards.ReviewCards
            .Select(card => card with
            {
                WasDueAtQueueStart = schedulerEligibleCardIds.Contains(card.Id),
            })
            .ToArray();

        return new AppReadModels.FlashcardReviewQueueReadModel(
            reviewCards,
            CreateCramSummary(
                ToReadModel(counts),
                reviewCards.Length,
                cardIds.Length,
                Math.Max(0, cardIds.Length - reviewCards.Length)))
        {
            Mode = AppReadModels.FlashcardReviewQueueMode.Cram,
        };
    }

    private static AppReadModels.FlashcardReviewSettingsReadModel ToReadModel(
        PersistenceSettings settings)
    {
        return new AppReadModels.FlashcardReviewSettingsReadModel(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention,
            settings.LearningSteps,
            settings.RelearningSteps,
            settings.IsOptimizationEnabled,
            settings.FsrsParameters).Normalize();
    }

    private static AppQueueCounts ToReadModel(PersistenceQueueCounts counts)
    {
        return new AppQueueCounts(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded)
        {
            FutureReviewCards = counts.FutureReviewCards,
        };
    }

    private static AppReadModels.FlashcardReviewQueueSummaryReadModel CreateCramSummary(
        AppQueueCounts counts,
        int queuedCards,
        int selectedCards,
        int skippedInvalidCards)
    {
        return new AppReadModels.FlashcardReviewQueueSummaryReadModel(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded,
            0,
            0,
            queuedCards)
        {
            FutureReviewCards = counts.FutureReviewCards,
            SelectedCards = selectedCards,
            SkippedInvalidCards = skippedInvalidCards,
        };
    }

    private static AppReviewLimitUsage ToReadModel(PersistenceReviewLimitUsage usage)
    {
        return new AppReviewLimitUsage(
            usage.ReviewCards,
            usage.NewCards);
    }

    private static AppQueueCandidate ToReadModel(PersistenceQueueCandidate candidate)
    {
        return new AppQueueCandidate(
            candidate.CardId,
            candidate.Category switch
            {
                PersistenceQueueCategory.Learning => AppQueueCategory.Learning,
                PersistenceQueueCategory.Relearning => AppQueueCategory.Relearning,
                PersistenceQueueCategory.Overdue => AppQueueCategory.Overdue,
                PersistenceQueueCategory.Due => AppQueueCategory.Due,
                PersistenceQueueCategory.New => AppQueueCategory.New,
                _ => AppQueueCategory.New,
            },
            candidate.DueUtc);
    }
}
