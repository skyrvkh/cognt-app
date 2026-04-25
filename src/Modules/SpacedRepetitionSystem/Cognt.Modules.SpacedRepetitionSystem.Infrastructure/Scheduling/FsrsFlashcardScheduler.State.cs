using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsFlashcardScheduler
{
    private static StoredFsrsCard CreateInitialStoredCard()
    {
        FsrsCard card = new(
            state: FsrsState.Learning,
            step: 0,
            due: DateTimeOffset.UtcNow);

        return new StoredFsrsCard(card, Reps: 0, Lapses: 0, ElapsedDays: 0, ScheduledDays: 0);
    }

    private static StoredFsrsCard CreateReviewedStoredCard(
        StoredFsrsCard previous,
        FsrsCard nextCard,
        FlashcardReviewRatingReadModel rating,
        int elapsedDays,
        DateTimeOffset reviewAt)
    {
        return new StoredFsrsCard(
            nextCard,
            previous.Reps + 1,
            rating == FlashcardReviewRatingReadModel.Again ? previous.Lapses + 1 : previous.Lapses,
            elapsedDays,
            CalculateScheduledDays(nextCard.Due, reviewAt));
    }

    private static FlashcardSchedule ToSchedule(
        StoredFsrsCard stored,
        string? reviewLog,
        FlashcardLearningState state)
    {
        return new FlashcardSchedule(
            FsrsStoredStateCodec.Serialize(stored),
            reviewLog,
            stored.Card.Due,
            state,
            stored.Card.Stability,
            stored.Card.Difficulty,
            stored.Card.Step,
            stored.Card.LastReview,
            stored.ElapsedDays,
            stored.ScheduledDays,
            stored.Reps,
            stored.Lapses);
    }

    private static int CalculateScheduledDays(DateTimeOffset dueUtc, DateTimeOffset fromUtc)
    {
        return NormalizeCount((int)Math.Floor((dueUtc - fromUtc).TotalDays));
    }

    private static int CalculateElapsedDays(DateTimeOffset? lastReviewUtc, DateTimeOffset reviewAtUtc)
    {
        return lastReviewUtc is null
            ? 0
            : NormalizeCount((reviewAtUtc - lastReviewUtc.Value).Days);
    }

    private static int NormalizeCount(int count)
    {
        return Math.Max(0, count);
    }

    private static FsrsRating ToFsrsRating(FlashcardReviewRatingReadModel rating)
    {
        return rating switch
        {
            FlashcardReviewRatingReadModel.Again => FsrsRating.Again,
            FlashcardReviewRatingReadModel.Hard => FsrsRating.Hard,
            FlashcardReviewRatingReadModel.Good => FsrsRating.Good,
            FlashcardReviewRatingReadModel.Easy => FsrsRating.Easy,
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Unsupported review rating.")
        };
    }

    private static FlashcardLearningState ToLearningState(FsrsState state)
    {
        return state switch
        {
            FsrsState.Learning => FlashcardLearningState.Learning,
            FsrsState.Relearning => FlashcardLearningState.Relearning,
            FsrsState.Review => FlashcardLearningState.Review,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported FSRS state.")
        };
    }
}
