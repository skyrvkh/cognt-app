namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public static partial class FlashcardReviewQueuePlanner
{
    private static long[] CreateCardIdQueue(
        IReadOnlyList<FlashcardReviewQueueCandidate> inProgressCandidates,
        IReadOnlyList<FlashcardReviewQueueCandidate> limitedReviewCandidates,
        IReadOnlyList<FlashcardReviewQueueCandidate> limitedNewCandidates)
    {
        long[] cardIds = new long[
            inProgressCandidates.Count
            + limitedReviewCandidates.Count
            + limitedNewCandidates.Count];
        int index = CopyCardIds(inProgressCandidates, cardIds, startIndex: 0);
        index = CopyCardIds(limitedReviewCandidates, cardIds, index);
        CopyCardIds(limitedNewCandidates, cardIds, index);
        return cardIds;
    }

    private static int CopyCardIds(
        IReadOnlyList<FlashcardReviewQueueCandidate> candidates,
        long[] cardIds,
        int startIndex)
    {
        int index = startIndex;
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            cardIds[index++] = candidates[candidateIndex].CardId;
        }

        return index;
    }
}
