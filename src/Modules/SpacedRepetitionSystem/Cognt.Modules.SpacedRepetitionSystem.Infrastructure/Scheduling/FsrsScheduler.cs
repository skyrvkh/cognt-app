namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler : IEquatable<FsrsScheduler>
{
    private readonly double decay;
    private readonly double factor;
    private readonly Random random;

    internal FsrsScheduler(
        IReadOnlyList<double>? parameters = null,
        double desiredRetention = 0.9,
        IReadOnlyList<TimeSpan>? learningSteps = null,
        IReadOnlyList<TimeSpan>? relearningSteps = null,
        int maximumInterval = 36500,
        bool enableFuzzing = true,
        Random? random = null)
    {
        parameters ??= DefaultParameters;
        ValidateParameters(parameters);
        Parameters = parameters.ToArray();
        DesiredRetention = desiredRetention;
        LearningSteps = learningSteps?.ToArray() ?? [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)];
        RelearningSteps = relearningSteps?.ToArray() ?? [TimeSpan.FromMinutes(10)];
        MaximumInterval = maximumInterval;
        EnableFuzzing = enableFuzzing;
        this.random = random ?? Random.Shared;
        decay = -Parameters[20];
        factor = Math.Pow(0.9, 1 / decay) - 1;
    }

    internal double[] Parameters { get; }

    internal double DesiredRetention { get; }

    internal TimeSpan[] LearningSteps { get; }

    internal TimeSpan[] RelearningSteps { get; }

    internal int MaximumInterval { get; }

    internal bool EnableFuzzing { get; }

    internal double GetCardRetrievability(FsrsCard card, DateTimeOffset? currentDatetime = null)
    {
        if (card.LastReview is null || card.Stability is null)
        {
            return 0;
        }

        DateTimeOffset current = currentDatetime ?? DateTimeOffset.UtcNow;
        int elapsedDays = Math.Max(0, (current - card.LastReview.Value).Days);
        return Math.Pow(1 + factor * elapsedDays / card.Stability!.Value, decay);
    }

    internal (FsrsCard Card, FsrsReviewLog ReviewLog) ReviewCard(
        FsrsCard card,
        FsrsRating rating,
        DateTimeOffset? reviewDatetime = null,
        int? reviewDuration = null)
    {
        if (reviewDatetime is not null && reviewDatetime.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("datetime must be timezone-aware and set to UTC", nameof(reviewDatetime));
        }

        card = card.Copy();
        DateTimeOffset reviewAt = reviewDatetime ?? DateTimeOffset.UtcNow;
        int? daysSinceLastReview = card.LastReview is null ? null : (reviewAt - card.LastReview.Value).Days;
        TimeSpan nextInterval = card.State switch
        {
            FsrsState.Learning => ReviewLearningCard(card, rating, reviewAt, daysSinceLastReview),
            FsrsState.Review => ReviewReviewCard(card, rating, reviewAt, daysSinceLastReview),
            FsrsState.Relearning => ReviewRelearningCard(card, rating, reviewAt, daysSinceLastReview),
            _ => throw new ArgumentOutOfRangeException(nameof(card.State))
        };

        if (EnableFuzzing && card.State == FsrsState.Review)
        {
            nextInterval = GetFuzzedInterval(nextInterval);
        }

        card.Due = reviewAt + nextInterval;
        card.LastReview = reviewAt;

        FsrsReviewLog reviewLog = new(card.CardId, rating, reviewAt, reviewDuration);
        return (card, reviewLog);
    }

    internal FsrsCard RescheduleCard(FsrsCard card, IReadOnlyList<FsrsReviewLog> reviewLogs)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(reviewLogs);

        foreach (FsrsReviewLog reviewLog in reviewLogs)
        {
            if (reviewLog.CardId != card.CardId)
            {
                throw new ArgumentException(
                    $"ReviewLog card_id {reviewLog.CardId} does not match Card card_id {card.CardId}",
                    nameof(reviewLogs));
            }
        }

        FsrsCard rescheduledCard = new(cardId: card.CardId, due: card.Due);
        foreach (FsrsReviewLog reviewLog in reviewLogs.OrderBy(log => log.ReviewDatetime))
        {
            (rescheduledCard, _) = ReviewCard(
                rescheduledCard,
                reviewLog.Rating,
                reviewLog.ReviewDatetime);
        }

        return rescheduledCard;
    }
}
