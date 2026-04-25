namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    private TimeSpan ReviewLearningCard(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset reviewAt,
        int? daysSinceLastReview)
    {
        if (card.Step is null)
        {
            throw new InvalidOperationException("Learning cards require a step.");
        }

        if (card.Stability is null || card.Difficulty is null)
        {
            card.Stability = InitialStability(rating);
            card.Difficulty = InitialDifficulty(rating, clamp: true);
        }
        else
        {
            UpdateScheduledCardState(card, rating, reviewAt, daysSinceLastReview);
        }

        if (LearningSteps.Length == 0 || (card.Step >= LearningSteps.Length && rating is FsrsRating.Hard or FsrsRating.Good or FsrsRating.Easy))
        {
            return PromoteCardToReview(card);
        }

        return NextLearningInterval(card, rating, LearningSteps);
    }

    private TimeSpan ReviewReviewCard(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset reviewAt,
        int? daysSinceLastReview)
    {
        if (card.Stability is null || card.Difficulty is null)
        {
            throw new InvalidOperationException("Review cards require stability and difficulty.");
        }

        UpdateReviewCardState(card, rating, reviewAt, daysSinceLastReview);

        if (rating == FsrsRating.Again && RelearningSteps.Length > 0)
        {
            card.State = FsrsState.Relearning;
            card.Step = 0;
            return RelearningSteps[card.Step.Value];
        }

        return TimeSpan.FromDays(NextInterval(card.Stability.Value));
    }

    private TimeSpan ReviewRelearningCard(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset reviewAt,
        int? daysSinceLastReview)
    {
        if (card.Stability is null || card.Difficulty is null || card.Step is null)
        {
            throw new InvalidOperationException("Relearning cards require stability, difficulty, and step.");
        }

        UpdateScheduledCardState(card, rating, reviewAt, daysSinceLastReview);

        if (RelearningSteps.Length == 0 || (card.Step >= RelearningSteps.Length && rating is FsrsRating.Hard or FsrsRating.Good or FsrsRating.Easy))
        {
            return PromoteCardToReview(card);
        }

        return NextLearningInterval(card, rating, RelearningSteps);
    }

    private void UpdateScheduledCardState(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset reviewAt,
        int? daysSinceLastReview)
    {
        if (daysSinceLastReview is not null && daysSinceLastReview < 1)
        {
            card.Stability = ShortTermStability(card.Stability!.Value, rating);
            card.Difficulty = NextDifficulty(card.Difficulty!.Value, rating);
            return;
        }

        card.Stability = NextStability(
            card.Difficulty!.Value,
            card.Stability!.Value,
            GetCardRetrievability(card, reviewAt),
            rating);
        card.Difficulty = NextDifficulty(card.Difficulty.Value, rating);
    }

    private void UpdateReviewCardState(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset reviewAt,
        int? daysSinceLastReview)
    {
        if (daysSinceLastReview is not null && daysSinceLastReview < 1)
        {
            card.Stability = ShortTermStability(card.Stability!.Value, rating);
        }
        else
        {
            card.Stability = NextStability(
                card.Difficulty!.Value,
                card.Stability!.Value,
                GetCardRetrievability(card, reviewAt),
                rating);
        }

        card.Difficulty = NextDifficulty(card.Difficulty!.Value, rating);
    }

}
