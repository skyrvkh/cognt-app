namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial class CramSessionEngine
{
    private static int EffectivePasses(
        CramPassCountingMode mode,
        int acceptedPasses,
        int consecutivePasses)
    {
        return mode == CramPassCountingMode.Consecutive
            ? consecutivePasses
            : acceptedPasses;
    }

    private static int CountOtherUnmasteredCards(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId,
        long excludedCardId)
    {
        int count = 0;
        foreach (CramCardProgress progress in progressByCardId.Values)
        {
            if (progress.CardId != excludedCardId && !progress.IsMastered)
            {
                count++;
            }
        }

        return count;
    }
}
