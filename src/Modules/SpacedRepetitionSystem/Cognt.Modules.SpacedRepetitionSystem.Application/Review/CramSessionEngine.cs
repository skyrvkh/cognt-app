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
        long[] distinctCardIds = CreateDistinctPositiveCardIds(cardIds);
        Dictionary<long, CramCardProgress> progress = CreateInitialProgress(distinctCardIds);
        Dictionary<long, int> nextEligibleTransitionByCardId = CreateInitialEligibility(distinctCardIds);
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
            CreatePendingCardIds(queue),
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

        Dictionary<long, CramCardProgress> nextProgressByCardId = CopyProgressByCardId(state.ProgressByCardId);
        nextProgressByCardId[cardId] = nextProgress;

        int nextTransitionCount = state.TransitionCount + 1;
        Dictionary<long, int> nextEligibleTransitionByCardId =
            CopyNextEligibleTransitionByCardId(state.NextEligibleTransitionByCardId);
        if (!mastered)
        {
            int otherUnmasteredCardCount = CountOtherUnmasteredCards(nextProgressByCardId, cardId);
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
            PendingCardIds = CreatePendingCardIds(nextQueue),
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

    private static long[] CreatePendingCardIds(IReadOnlyList<long> queue)
    {
        if (queue.Count <= 1)
        {
            return [];
        }

        long[] pendingCardIds = new long[queue.Count - 1];
        for (int index = 1; index < queue.Count; index++)
        {
            pendingCardIds[index - 1] = queue[index];
        }

        return pendingCardIds;
    }

    private static IReadOnlyList<long> BuildQueue(
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
                    previousOrderByCardId),
                activeCards.Count);
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
                previousOrderByCardId),
            activeCards.Count);
    }

    private static List<CramCardProgress> CreateActiveCards(
        IReadOnlyDictionary<long, CramCardProgress> progressByCardId)
    {
        List<CramCardProgress> activeCards = new(progressByCardId.Count);
        foreach (CramCardProgress progress in progressByCardId.Values)
        {
            if (!progress.IsMastered)
            {
                activeCards.Add(progress);
            }
        }

        return activeCards;
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

    private static long[] CreateCardIdQueue(IEnumerable<CramCardProgress> orderedCards, int count)
    {
        long[] cardIds = new long[count];
        int index = 0;
        foreach (CramCardProgress progress in orderedCards)
        {
            cardIds[index++] = progress.CardId;
        }

        return cardIds;
    }

    private static long[] CreateCardIdQueue(
        IEnumerable<CramCardProgress> firstOrderedCards,
        IEnumerable<CramCardProgress> secondOrderedCards,
        int count)
    {
        long[] cardIds = new long[count];
        int index = CopyCardIds(firstOrderedCards, cardIds, startIndex: 0);
        CopyCardIds(secondOrderedCards, cardIds, index);
        return cardIds;
    }

    private static int CopyCardIds(
        IEnumerable<CramCardProgress> source,
        long[] destination,
        int startIndex)
    {
        int index = startIndex;
        foreach (CramCardProgress progress in source)
        {
            destination[index++] = progress.CardId;
        }

        return index;
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
