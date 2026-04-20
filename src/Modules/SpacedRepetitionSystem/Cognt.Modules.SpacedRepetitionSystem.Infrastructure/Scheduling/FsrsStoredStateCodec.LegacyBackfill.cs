using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static partial class FsrsStoredStateCodec
{
    private static StoredFsrsCard CreateStoredCardFromFsrsCard(FsrsCard fsrsCard)
    {
        return new StoredFsrsCard(
            fsrsCard,
            Reps: fsrsCard.LastReview is null ? 0 : 1,
            Lapses: 0,
            ElapsedDays: 0,
            ScheduledDays: CalculateScheduledDays(fsrsCard.Due, DateTimeOffset.UtcNow));
    }

    private static StoredFsrsCard CreateDefaultBackfilledCard()
    {
        DateTimeOffset backfilledAt = DateTimeOffset.UtcNow;
        return CreateBackfilledCard(backfilledAt, FlashcardLearningState.New, null, null, 0, 0, backfilledAt);
    }

    private static StoredFsrsCard CreateBackfilledCard(
        DateTimeOffset dueUtc,
        FlashcardLearningState state,
        double? stability,
        double? difficulty,
        int reps,
        int lapses,
        DateTimeOffset backfilledAt)
    {
        FsrsState fsrsState = ToFsrsState(state, stability, difficulty);
        int? step = fsrsState is FsrsState.Learning or FsrsState.Relearning ? 0 : null;

        FsrsCard card = new(
            state: fsrsState,
            step: step,
            stability: fsrsState == FsrsState.Review ? stability ?? StabilityMin : stability,
            difficulty: fsrsState == FsrsState.Review ? difficulty ?? MaxDifficulty : difficulty,
            due: dueUtc,
            lastReview: reps > 0 ? dueUtc : null);

        return new StoredFsrsCard(
            card,
            NormalizeCount(reps),
            NormalizeCount(lapses),
            ElapsedDays: 0,
            ScheduledDays: CalculateScheduledDays(dueUtc, backfilledAt));
    }

    private static int CalculateScheduledDays(DateTimeOffset dueUtc, DateTimeOffset fromUtc)
    {
        return NormalizeCount((int)Math.Floor((dueUtc - fromUtc).TotalDays));
    }

    private static FsrsState ToFsrsState(
        FlashcardLearningState state,
        double? stability,
        double? difficulty)
    {
        return state switch
        {
            FlashcardLearningState.Review when stability.HasValue && difficulty.HasValue => FsrsState.Review,
            FlashcardLearningState.Relearning => FsrsState.Relearning,
            _ => FsrsState.Learning
        };
    }
}
