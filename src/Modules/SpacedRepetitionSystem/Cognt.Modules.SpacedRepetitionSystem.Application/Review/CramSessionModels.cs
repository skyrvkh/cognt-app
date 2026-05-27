using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed record CramCardProgress(
    long CardId,
    int TotalAttempts,
    int AcceptedPasses,
    int ConsecutivePasses,
    FlashcardReviewRatingReadModel? LastRating,
    bool IsMastered)
{
    public int EffectivePasses(CramPassCountingMode mode)
    {
        return mode == CramPassCountingMode.Consecutive
            ? ConsecutivePasses
            : AcceptedPasses;
    }
}

public sealed record CramCompletionSummary(
    int CardsSelected,
    int CardsMastered,
    int CardsSkippedInvalid,
    int TotalAttempts,
    int AgainRatings,
    int HardRatings,
    int GoodRatings,
    int EasyRatings,
    TimeSpan SessionDuration,
    int RequiredPassCount,
    CramPassCountingMode PassCountingMode);

public sealed record CramSessionState(
    CrammingSettings Settings,
    DateTimeOffset StartedAt,
    int CardsSelected,
    int CardsSkippedInvalid,
    long? CurrentCardId,
    IReadOnlyList<long> PendingCardIds,
    IReadOnlyDictionary<long, int> NextEligibleTransitionByCardId,
    IReadOnlyDictionary<long, CramCardProgress> ProgressByCardId,
    int TotalAttempts,
    int AgainRatings,
    int HardRatings,
    int GoodRatings,
    int EasyRatings,
    int RandomSeed,
    int TransitionCount)
{
    public int ValidCardCount => ProgressByCardId.Count;

    public int MasteredCardCount
    {
        get
        {
            int count = 0;
            foreach (CramCardProgress progress in ProgressByCardId.Values)
            {
                if (progress.IsMastered)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int RemainingCardCount => ValidCardCount - MasteredCardCount;

    public bool IsEmpty => ValidCardCount == 0;

    public bool IsComplete => ValidCardCount > 0 && RemainingCardCount == 0 && CurrentCardId is null;

    public CramCardProgress? CurrentProgress =>
        CurrentCardId.HasValue && ProgressByCardId.TryGetValue(CurrentCardId.Value, out CramCardProgress? progress)
            ? progress
            : null;

    public IReadOnlyList<long> ActiveCardIds =>
        CurrentCardId.HasValue
            ? CreateActiveCardIds(CurrentCardId.Value, PendingCardIds)
            : CopyCardIds(PendingCardIds);

    public CramCompletionSummary CreateCompletionSummary(DateTimeOffset completedAt)
    {
        return new CramCompletionSummary(
            CardsSelected,
            MasteredCardCount,
            CardsSkippedInvalid,
            TotalAttempts,
            AgainRatings,
            HardRatings,
            GoodRatings,
            EasyRatings,
            completedAt - StartedAt,
            Settings.RequiredPassCount,
            Settings.PassCountingMode);
    }

    private static long[] CreateActiveCardIds(long currentCardId, IReadOnlyList<long> pendingCardIds)
    {
        long[] activeCardIds = new long[pendingCardIds.Count + 1];
        activeCardIds[0] = currentCardId;
        CopyCardIds(pendingCardIds, activeCardIds, destinationStartIndex: 1);
        return activeCardIds;
    }

    private static long[] CopyCardIds(IReadOnlyList<long> cardIds)
    {
        long[] copy = new long[cardIds.Count];
        CopyCardIds(cardIds, copy, destinationStartIndex: 0);
        return copy;
    }

    private static void CopyCardIds(
        IReadOnlyList<long> source,
        long[] destination,
        int destinationStartIndex)
    {
        for (int index = 0; index < source.Count; index++)
        {
            destination[destinationStartIndex + index] = source[index];
        }
    }
}

public sealed record CramRatingResult(
    CramSessionState State,
    CramCardProgress RatedCardProgress,
    bool RatingAccepted,
    bool CardMastered,
    CramCompletionSummary? CompletionSummary);
