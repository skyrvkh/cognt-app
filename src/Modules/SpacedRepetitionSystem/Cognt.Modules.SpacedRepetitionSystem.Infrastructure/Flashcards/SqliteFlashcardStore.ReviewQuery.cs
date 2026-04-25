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

    internal AppReadModels.FlashcardReviewQueueReadModel LoadReviewQueue(string collectionId)
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

    private static AppReadModels.FlashcardReviewSettingsReadModel ToReadModel(
        PersistenceSettings settings)
    {
        return new AppReadModels.FlashcardReviewSettingsReadModel(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention).Normalize();
    }

    private static AppQueueCounts ToReadModel(PersistenceQueueCounts counts)
    {
        return new AppQueueCounts(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded);
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
