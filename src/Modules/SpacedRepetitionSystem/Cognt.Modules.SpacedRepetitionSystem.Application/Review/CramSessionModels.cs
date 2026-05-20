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

    public int MasteredCardCount => ProgressByCardId.Values.Count(progress => progress.IsMastered);

    public int RemainingCardCount => ValidCardCount - MasteredCardCount;

    public bool IsEmpty => ValidCardCount == 0;

    public bool IsComplete => ValidCardCount > 0 && RemainingCardCount == 0 && CurrentCardId is null;

    public CramCardProgress? CurrentProgress =>
        CurrentCardId.HasValue && ProgressByCardId.TryGetValue(CurrentCardId.Value, out CramCardProgress? progress)
            ? progress
            : null;

    public IReadOnlyList<long> ActiveCardIds =>
        CurrentCardId.HasValue
            ? new[] { CurrentCardId.Value }.Concat(PendingCardIds).ToArray()
            : PendingCardIds.ToArray();

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
}

public sealed record CramRatingResult(
    CramSessionState State,
    CramCardProgress RatedCardProgress,
    bool RatingAccepted,
    bool CardMastered,
    CramCompletionSummary? CompletionSummary);
