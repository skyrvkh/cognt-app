using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial class CramSessionEngine : ICramSessionEngine
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
        IReadOnlyList<long> queue = CramSessionQueuePlanner.BuildQueue(
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
            CramSessionQueuePlanner.CreatePendingCardIds(queue),
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

        IReadOnlyList<long> nextQueue = CramSessionQueuePlanner.BuildQueue(
            nextProgressByCardId,
            nextEligibleTransitionByCardId,
            state.Settings,
            state.RandomSeed,
            nextTransitionCount,
            state.ActiveCardIds);

        CramSessionState nextState = state with
        {
            CurrentCardId = nextQueue.Count == 0 ? null : nextQueue[0],
            PendingCardIds = CramSessionQueuePlanner.CreatePendingCardIds(nextQueue),
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
}
