using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public static partial class FlashcardReviewQueuePlanner
{
    private static IReadOnlyList<FlashcardReviewQueueCandidate> SelectCandidates(
        IEnumerable<FlashcardReviewQueueCandidate> candidates,
        Func<FlashcardReviewQueueCategory, bool> isIncludedCategory,
        int? limit = null)
    {
        IOrderedEnumerable<FlashcardReviewQueueCandidate> orderedCandidates = OrderByQueuePosition(candidates
            .Where(candidate => isIncludedCategory(candidate.Category)));

        return limit.HasValue
            ? orderedCandidates.Take(limit.Value).ToArray()
            : orderedCandidates.ToArray();
    }

    private static int RemainingDailyLimit(int limit, int used)
    {
        return Math.Max(0, limit - used);
    }

    private static FlashcardReviewQueueSummaryReadModel CreateSummary(
        FlashcardReviewQueueCounts counts,
        int selectedReviewCards,
        int selectedNewCards,
        int queuedCards)
    {
        int newLimitExcluded = Math.Max(0, counts.NewCards - selectedNewCards);
        int reviewLimitExcluded = Math.Max(0, counts.ReviewDueCards - selectedReviewCards);

        return new FlashcardReviewQueueSummaryReadModel(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded,
            reviewLimitExcluded,
            newLimitExcluded,
            queuedCards);
    }

    private static IOrderedEnumerable<FlashcardReviewQueueCandidate> OrderByQueuePosition(
        IEnumerable<FlashcardReviewQueueCandidate> candidates)
    {
        return candidates
            .OrderBy(candidate => candidate.Category)
            .ThenBy(candidate => candidate.DueUtc)
            .ThenBy(candidate => candidate.CardId);
    }

    private static bool IsInProgressCategory(FlashcardReviewQueueCategory category)
    {
        return category is FlashcardReviewQueueCategory.Learning
            or FlashcardReviewQueueCategory.Relearning;
    }

    private static bool IsReviewCategory(FlashcardReviewQueueCategory category)
    {
        return category is FlashcardReviewQueueCategory.Overdue
            or FlashcardReviewQueueCategory.Due;
    }

    private static bool IsNewCategory(FlashcardReviewQueueCategory category)
    {
        return category is FlashcardReviewQueueCategory.New;
    }
}
