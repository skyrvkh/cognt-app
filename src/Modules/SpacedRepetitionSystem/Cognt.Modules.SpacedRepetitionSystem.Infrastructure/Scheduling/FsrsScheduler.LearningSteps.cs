namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    private TimeSpan PromoteCardToReview(FsrsCard card)
    {
        card.State = FsrsState.Review;
        card.Step = null;
        return TimeSpan.FromDays(NextInterval(card.Stability!.Value));
    }

    private TimeSpan NextLearningInterval(FsrsCard card, FsrsRating rating, TimeSpan[] steps)
    {
        switch (rating)
        {
            case FsrsRating.Again:
                card.Step = 0;
                return steps[card.Step.GetValueOrDefault()];

            case FsrsRating.Hard:
                if (card.Step == 0 && steps.Length == 1)
                {
                    return steps[0] * 1.5;
                }

                if (card.Step == 0 && steps.Length >= 2)
                {
                    return (steps[0] + steps[1]) / 2.0;
                }

                return steps[card.Step.GetValueOrDefault()];

            case FsrsRating.Good:
                if (card.Step + 1 == steps.Length)
                {
                    return PromoteCardToReview(card);
                }

                card.Step += 1;
                return steps[card.Step.GetValueOrDefault()];

            case FsrsRating.Easy:
                return PromoteCardToReview(card);

            default:
                throw new ArgumentOutOfRangeException(nameof(rating));
        }
    }
}
