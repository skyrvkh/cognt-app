namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public static partial class FlashcardReviewQueuePlanner
{
    private static int RemainingDailyLimit(int limit, int used)
    {
        return Math.Max(0, limit - used);
    }
}
