namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial class CramSessionEngine
{
    private static Dictionary<long, CramCardProgress> CopyProgressByCardId(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId)
    {
        Dictionary<long, CramCardProgress> copy = new(progressByCardId.Count);
        foreach (KeyValuePair<long, CramCardProgress> pair in progressByCardId)
        {
            copy.Add(pair.Key, pair.Value);
        }

        return copy;
    }

    private static Dictionary<long, int> CopyNextEligibleTransitionByCardId(
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId)
    {
        Dictionary<long, int> copy = new(nextEligibleTransitionByCardId.Count);
        foreach (KeyValuePair<long, int> pair in nextEligibleTransitionByCardId)
        {
            copy.Add(pair.Key, pair.Value);
        }

        return copy;
    }
}
