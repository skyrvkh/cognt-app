using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public static partial class FlashcardReviewQueuePlanner
{
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
}
