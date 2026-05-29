namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static class FsrsOptimizerRetentionSearch
{
    private const int SimulationCardCount = 1000;
    private static readonly double[] RetentionCandidates = [0.70d, 0.75d, 0.80d, 0.85d, 0.90d, 0.95d];

    internal static double Compute(
        IReadOnlyList<double> parameters,
        IReadOnlyList<FsrsReviewLog> reviewLogs,
        IReadOnlyDictionary<long, FsrsReviewLog[]> reviewLogsByCardId,
        int minimumReviewLogCount)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(reviewLogs);
        ArgumentNullException.ThrowIfNull(reviewLogsByCardId);

        if (reviewLogs.Count < minimumReviewLogCount)
        {
            throw new InvalidOperationException(
                $"At least {minimumReviewLogCount} review logs are required to compute optimal retention.");
        }

        if (reviewLogs.Any(log => log.ReviewDuration is null))
        {
            throw new InvalidOperationException("Every review log must have a duration to compute optimal retention.");
        }

        FsrsOptimizerReviewStatistics statistics = FsrsOptimizerReviewStatistics.Create(
            reviewLogs,
            reviewLogsByCardId);
        return RetentionCandidates.MinBy(retention => SimulateCost(parameters, retention, statistics));
    }

    private static double SimulateCost(
        IReadOnlyList<double> parameters,
        double desiredRetention,
        FsrsOptimizerReviewStatistics statistics)
    {
        DateTimeOffset startDate = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset endDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Random random = new(42);
        double totalCost = 0;

        for (int cardIndex = 0; cardIndex < SimulationCardCount; cardIndex++)
        {
            FsrsScheduler scheduler = new(
                parameters,
                desiredRetention,
                enableFuzzing: false,
                random: random);
            FsrsCard card = new(cardId: cardIndex + 1, due: startDate);
            DateTimeOffset reviewDate = startDate;
            int reviewGuard = 0;

            while (reviewDate < endDate && reviewGuard < 10_000)
            {
                FsrsRating rating = card.LastReview is null
                    ? ChooseFirstReviewRating(statistics, random)
                    : ChooseReviewRating(statistics, desiredRetention, random);
                totalCost += statistics.CostFor(rating);
                (card, _) = scheduler.ReviewCard(
                    card,
                    rating,
                    reviewDate,
                    (int)Math.Round(statistics.CostFor(rating)));
                reviewDate = card.Due;
                reviewGuard++;
            }
        }

        return totalCost / (desiredRetention * SimulationCardCount);
    }

    private static FsrsRating ChooseFirstReviewRating(FsrsOptimizerReviewStatistics statistics, Random random)
        => ChooseWeighted(
            random,
            (FsrsRating.Again, statistics.FirstAgainProbability),
            (FsrsRating.Hard, statistics.FirstHardProbability),
            (FsrsRating.Good, statistics.FirstGoodProbability),
            (FsrsRating.Easy, statistics.FirstEasyProbability));

    private static FsrsRating ChooseReviewRating(
        FsrsOptimizerReviewStatistics statistics,
        double desiredRetention,
        Random random)
        => random.NextDouble() >= desiredRetention
            ? FsrsRating.Again
            : ChooseWeighted(
            random,
            (FsrsRating.Hard, statistics.RecallHardProbability),
            (FsrsRating.Good, statistics.RecallGoodProbability),
            (FsrsRating.Easy, statistics.RecallEasyProbability));

    private static FsrsRating ChooseWeighted(
        Random random,
        params (FsrsRating Rating, double Weight)[] options)
    {
        double totalWeight = options.Sum(option => Math.Max(0, option.Weight));
        if (totalWeight <= 0)
        {
            return FsrsRating.Good;
        }

        double position = random.NextDouble() * totalWeight;
        foreach ((FsrsRating rating, double weight) in options)
        {
            position -= Math.Max(0, weight);
            if (position <= 0)
            {
                return rating;
            }
        }

        return options[^1].Rating;
    }
}
