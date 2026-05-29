namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

internal static partial class CramSessionQueuePlanner
{
    private static IOrderedEnumerable<CramCardProgress> OrderEligibleCards(
        IEnumerable<CramCardProgress> cards,
        CrammingSettings settings,
        int randomSeed,
        int transitionCount,
        IReadOnlyDictionary<long, int> previousOrderByCardId)
    {
        IOrderedEnumerable<CramCardProgress> ordered = cards
            .OrderBy(progress => progress.TotalAttempts);

        return ThenByCue(
            ordered,
            settings,
            randomSeed,
            transitionCount,
            previousOrderByCardId);
    }

    private static IOrderedEnumerable<CramCardProgress> OrderWaitingCards(
        IEnumerable<CramCardProgress> cards,
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId,
        CrammingSettings settings,
        int randomSeed,
        int transitionCount,
        IReadOnlyDictionary<long, int> previousOrderByCardId)
    {
        IOrderedEnumerable<CramCardProgress> ordered = cards
            .OrderBy(progress => GetNextEligibleTransition(
                nextEligibleTransitionByCardId,
                progress.CardId))
            .ThenBy(progress => progress.TotalAttempts);

        return ThenByCue(
            ordered,
            settings,
            randomSeed,
            transitionCount,
            previousOrderByCardId);
    }

    private static IOrderedEnumerable<CramCardProgress> ThenByCue(
        IOrderedEnumerable<CramCardProgress> ordered,
        CrammingSettings settings,
        int randomSeed,
        int transitionCount,
        IReadOnlyDictionary<long, int> previousOrderByCardId)
    {
        if (settings.ShuffleRemainingCards)
        {
            return ordered
                .ThenBy(progress => GetRandomCue(randomSeed, transitionCount, progress.CardId))
                .ThenBy(progress => progress.CardId);
        }

        return ordered
            .ThenBy(progress => GetPreviousOrder(previousOrderByCardId, progress.CardId))
            .ThenBy(progress => progress.CardId);
    }

    private static int GetNextEligibleTransition(
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId,
        long cardId)
    {
        return nextEligibleTransitionByCardId.TryGetValue(cardId, out int transition)
            ? transition
            : 0;
    }

    private static int GetPreviousOrder(
        IReadOnlyDictionary<long, int> previousOrderByCardId,
        long cardId)
    {
        return previousOrderByCardId.TryGetValue(cardId, out int order)
            ? order
            : int.MaxValue;
    }

    private static ulong GetRandomCue(
        int randomSeed,
        int transitionCount,
        long cardId)
    {
        unchecked
        {
            ulong value = (ulong)cardId;
            value += 0x9E3779B97F4A7C15UL;
            value ^= (uint)randomSeed;
            value += ((ulong)(uint)transitionCount << 32) ^ (uint)transitionCount;
            return Mix(value);
        }
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return value;
        }
    }
}
