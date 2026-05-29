namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial class CramSessionEngine
{
    private static long[] CreateDistinctPositiveCardIds(IReadOnlyList<long> cardIds)
    {
        List<long> distinctCardIds = new(cardIds.Count);
        HashSet<long> seenCardIds = new();
        foreach (long cardId in cardIds)
        {
            if (cardId > 0 && seenCardIds.Add(cardId))
            {
                distinctCardIds.Add(cardId);
            }
        }

        return distinctCardIds.ToArray();
    }

    private static Dictionary<long, CramCardProgress> CreateInitialProgress(IReadOnlyList<long> cardIds)
    {
        Dictionary<long, CramCardProgress> progressByCardId = new(cardIds.Count);
        foreach (long cardId in cardIds)
        {
            progressByCardId.Add(cardId, new CramCardProgress(cardId, 0, 0, 0, null, false));
        }

        return progressByCardId;
    }

    private static Dictionary<long, int> CreateInitialEligibility(IReadOnlyList<long> cardIds)
    {
        Dictionary<long, int> nextEligibleTransitionByCardId = new(cardIds.Count);
        foreach (long cardId in cardIds)
        {
            nextEligibleTransitionByCardId.Add(cardId, 0);
        }

        return nextEligibleTransitionByCardId;
    }
}
