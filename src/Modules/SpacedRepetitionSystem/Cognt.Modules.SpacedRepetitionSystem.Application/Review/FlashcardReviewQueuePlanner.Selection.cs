namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public static partial class FlashcardReviewQueuePlanner
{
    private static IReadOnlyList<FlashcardReviewQueueCandidate> SelectCandidates(
        IEnumerable<FlashcardReviewQueueCandidate> candidates,
        Func<FlashcardReviewQueueCategory, bool> isIncludedCategory,
        int? limit = null)
    {
        List<FlashcardReviewQueueCandidate> selectedCandidates = [];
        foreach (FlashcardReviewQueueCandidate candidate in candidates)
        {
            if (isIncludedCategory(candidate.Category))
            {
                selectedCandidates.Add(candidate);
            }
        }

        selectedCandidates.Sort(CompareQueuePosition);

        int selectedCount = limit.HasValue
            ? Math.Min(limit.Value, selectedCandidates.Count)
            : selectedCandidates.Count;
        if (selectedCount == 0)
        {
            return [];
        }

        FlashcardReviewQueueCandidate[] result = new FlashcardReviewQueueCandidate[selectedCount];
        for (int index = 0; index < selectedCount; index++)
        {
            result[index] = selectedCandidates[index];
        }

        return result;
    }

    private static int CompareQueuePosition(
        FlashcardReviewQueueCandidate left,
        FlashcardReviewQueueCandidate right)
    {
        int categoryComparison = left.Category.CompareTo(right.Category);
        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        int dueComparison = left.DueUtc.CompareTo(right.DueUtc);
        return dueComparison != 0
            ? dueComparison
            : left.CardId.CompareTo(right.CardId);
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
