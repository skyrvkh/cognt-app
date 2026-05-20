using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed class CramSessionEngine : ICramSessionEngine
{
    private const int DefaultRandomSeed = 48271;

    public CramSessionState Start(
        IReadOnlyList<long> cardIds,
        CrammingSettings settings)
    {
        return Start(cardIds, settings, cardIds.Count, 0);
    }

    public CramSessionState Start(
        IReadOnlyList<long> cardIds,
        CrammingSettings settings,
        int cardsSelected,
        int cardsSkippedInvalid)
    {
        ArgumentNullException.ThrowIfNull(cardIds);
        ArgumentNullException.ThrowIfNull(settings);

        settings = settings.EnsureValid();
        long[] distinctCardIds = cardIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        Dictionary<long, CramCardProgress> progress = distinctCardIds.ToDictionary(
            id => id,
            id => new CramCardProgress(id, 0, 0, 0, null, false));
        Dictionary<long, int> nextEligibleTransitionByCardId = distinctCardIds.ToDictionary(
            id => id,
            _ => 0);
        IReadOnlyList<long> queue = BuildQueue(
            progress,
            nextEligibleTransitionByCardId,
            settings,
            DefaultRandomSeed,
            transitionCount: 0,
            distinctCardIds);

        return new CramSessionState(
            settings,
            DateTimeOffset.UtcNow,
            Math.Max(cardsSelected, distinctCardIds.Length),
            Math.Max(0, cardsSkippedInvalid),
            queue.Count == 0 ? null : queue[0],
            queue.Skip(1).ToArray(),
            nextEligibleTransitionByCardId,
            progress,
            TotalAttempts: 0,
            AgainRatings: 0,
            HardRatings: 0,
            GoodRatings: 0,
            EasyRatings: 0,
            DefaultRandomSeed,
            TransitionCount: 0);
    }

    public CramRatingResult RateCurrent(
        CramSessionState state,
        FlashcardReviewRatingReadModel rating,
        DateTimeOffset? ratedAt = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Settings.EnsureValid();

        long cardId = state.CurrentCardId
            ?? throw new InvalidOperationException("There is no active cram card to rate.");
        if (!state.ProgressByCardId.TryGetValue(cardId, out CramCardProgress? currentProgress))
        {
            throw new InvalidOperationException($"Cram card {cardId} is not part of the active session.");
        }

        bool accepted = state.Settings.IsAcceptedRating(rating);
        int acceptedPasses = currentProgress.AcceptedPasses + (accepted ? 1 : 0);
        int consecutivePasses = accepted
            ? currentProgress.ConsecutivePasses + 1
            : 0;
        bool mastered = EffectivePasses(state.Settings.PassCountingMode, acceptedPasses, consecutivePasses)
            >= state.Settings.RequiredPassCount;
        CramCardProgress nextProgress = currentProgress with
        {
            TotalAttempts = currentProgress.TotalAttempts + 1,
            AcceptedPasses = acceptedPasses,
            ConsecutivePasses = consecutivePasses,
            LastRating = rating,
            IsMastered = mastered,
        };

        Dictionary<long, CramCardProgress> nextProgressByCardId = state.ProgressByCardId
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        nextProgressByCardId[cardId] = nextProgress;

        int nextTransitionCount = state.TransitionCount + 1;
        Dictionary<long, int> nextEligibleTransitionByCardId = state.NextEligibleTransitionByCardId
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (!mastered)
        {
            int otherUnmasteredCardCount = nextProgressByCardId.Values
                .Count(progress => progress.CardId != cardId && !progress.IsMastered);
            int retrySpacing = Math.Min(state.Settings.RetryAfterOtherCards, otherUnmasteredCardCount);
            nextEligibleTransitionByCardId[cardId] = nextTransitionCount + retrySpacing;
        }
        else
        {
            nextEligibleTransitionByCardId.Remove(cardId);
        }

        IReadOnlyList<long> nextQueue = BuildQueue(
            nextProgressByCardId,
            nextEligibleTransitionByCardId,
            state.Settings,
            state.RandomSeed,
            nextTransitionCount,
            state.ActiveCardIds);

        CramSessionState nextState = state with
        {
            CurrentCardId = nextQueue.Count == 0 ? null : nextQueue[0],
            PendingCardIds = nextQueue.Skip(1).ToArray(),
            NextEligibleTransitionByCardId = nextEligibleTransitionByCardId,
            ProgressByCardId = nextProgressByCardId,
            TotalAttempts = state.TotalAttempts + 1,
            AgainRatings = state.AgainRatings + (rating == FlashcardReviewRatingReadModel.Again ? 1 : 0),
            HardRatings = state.HardRatings + (rating == FlashcardReviewRatingReadModel.Hard ? 1 : 0),
            GoodRatings = state.GoodRatings + (rating == FlashcardReviewRatingReadModel.Good ? 1 : 0),
            EasyRatings = state.EasyRatings + (rating == FlashcardReviewRatingReadModel.Easy ? 1 : 0),
            TransitionCount = nextTransitionCount,
        };

        CramCompletionSummary? summary = nextState.IsComplete
            ? nextState.CreateCompletionSummary(ratedAt ?? DateTimeOffset.UtcNow)
            : null;

        return new CramRatingResult(nextState, nextProgress, accepted, mastered, summary);
    }

    private static int EffectivePasses(
        CramPassCountingMode mode,
        int acceptedPasses,
        int consecutivePasses)
    {
        return mode == CramPassCountingMode.Consecutive
            ? consecutivePasses
            : acceptedPasses;
    }

    private static IReadOnlyList<long> BuildQueue(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId,
        IReadOnlyDictionary<long, int> nextEligibleTransitionByCardId,
        CrammingSettings settings,
        int randomSeed,
        int transitionCount,
        IReadOnlyList<long> previousOrder)
    {
        CramCardProgress[] activeCards = progressByCardId.Values
            .Where(progress => !progress.IsMastered)
            .ToArray();
        if (activeCards.Length == 0)
        {
            return [];
        }

        Dictionary<long, int> previousOrderByCardId = previousOrder
            .Select((cardId, index) => new { cardId, index })
            .GroupBy(item => item.cardId)
            .ToDictionary(group => group.Key, group => group.First().index);

        CramCardProgress[] eligibleCards = activeCards
            .Where(progress => GetNextEligibleTransition(
                nextEligibleTransitionByCardId,
                progress.CardId) <= transitionCount)
            .ToArray();

        if (eligibleCards.Length == 0)
        {
            return OrderWaitingCards(
                    activeCards,
                    nextEligibleTransitionByCardId,
                    settings,
                    randomSeed,
                    transitionCount,
                    previousOrderByCardId)
                .Select(progress => progress.CardId)
                .ToArray();
        }

        CramCardProgress[] waitingCards = activeCards
            .Where(progress => GetNextEligibleTransition(
                nextEligibleTransitionByCardId,
                progress.CardId) > transitionCount)
            .ToArray();

        return OrderEligibleCards(
                eligibleCards,
                settings,
                randomSeed,
                transitionCount,
                previousOrderByCardId)
            .Concat(OrderWaitingCards(
                waitingCards,
                nextEligibleTransitionByCardId,
                settings,
                randomSeed,
                transitionCount,
                previousOrderByCardId))
            .Select(progress => progress.CardId)
            .ToArray();
    }

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
