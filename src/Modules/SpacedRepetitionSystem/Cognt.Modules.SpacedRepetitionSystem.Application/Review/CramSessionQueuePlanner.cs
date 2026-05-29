namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

internal static partial class CramSessionQueuePlanner
{
    internal static long[] CreatePendingCardIds(IReadOnlyList<long> queue)
    {
        return queue.Skip(1).ToArray();
    }

    internal static IReadOnlyList<long> BuildQueue(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId,
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId,
        CrammingSettings settings,
        int randomSeed,
        int transitionCount,
        IReadOnlyList<long> previousOrder)
    {
        List<CramCardProgress> activeCards = CreateActiveCards(progressByCardId);
        if (activeCards.Count == 0)
        {
            return [];
        }

        Dictionary<long, int> previousOrderByCardId = CreatePreviousOrderIndex(previousOrder);
        (List<CramCardProgress> eligibleCards, List<CramCardProgress> waitingCards) =
            PartitionActiveCards(activeCards, nextEligibleTransitionByCardId, transitionCount);

        if (eligibleCards.Count == 0)
        {
            return CreateCardIdQueue(
                OrderWaitingCards(
                    activeCards,
                    nextEligibleTransitionByCardId,
                    settings,
                    randomSeed,
                    transitionCount,
                    previousOrderByCardId));
        }

        return CreateCardIdQueue(
            OrderEligibleCards(
                eligibleCards,
                settings,
                randomSeed,
                transitionCount,
                previousOrderByCardId),
            OrderWaitingCards(
                waitingCards,
                nextEligibleTransitionByCardId,
                settings,
                randomSeed,
                transitionCount,
                previousOrderByCardId));
    }

    private static List<CramCardProgress> CreateActiveCards(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId)
    {
        return progressByCardId.Values
            .Where(static progress => !progress.IsMastered)
            .ToList();
    }

    private static Dictionary<long, int> CreatePreviousOrderIndex(IReadOnlyList<long> previousOrder)
    {
        Dictionary<long, int> previousOrderByCardId = new(previousOrder.Count);
        for (int index = 0; index < previousOrder.Count; index++)
        {
            previousOrderByCardId.TryAdd(previousOrder[index], index);
        }

        return previousOrderByCardId;
    }

    private static (List<CramCardProgress> EligibleCards, List<CramCardProgress> WaitingCards) PartitionActiveCards(
        IReadOnlyList<CramCardProgress> activeCards,
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId,
        int transitionCount)
    {
        List<CramCardProgress> eligibleCards = [];
        List<CramCardProgress> waitingCards = [];
        foreach (CramCardProgress progress in activeCards)
        {
            if (GetNextEligibleTransition(nextEligibleTransitionByCardId, progress.CardId) <= transitionCount)
            {
                eligibleCards.Add(progress);
            }
            else
            {
                waitingCards.Add(progress);
            }
        }

        return (eligibleCards, waitingCards);
    }

    private static long[] CreateCardIdQueue(IEnumerable<CramCardProgress> orderedCards)
    {
        return orderedCards.Select(static progress => progress.CardId).ToArray();
    }

    private static long[] CreateCardIdQueue(
        IEnumerable<CramCardProgress> firstOrderedCards,
        IEnumerable<CramCardProgress> secondOrderedCards)
    {
        return firstOrderedCards
            .Concat(secondOrderedCards)
            .Select(static progress => progress.CardId)
            .ToArray();
    }

}
