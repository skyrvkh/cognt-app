namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed class FsrsOptimizer
{
    private const int NumberOfEpochs = 5;
    private const int MinimumReviewLogCount = 512;
    private const int MaximumSequenceLength = 64;
    private const int SimulationCardCount = 1000;
    private static readonly double[] RetentionCandidates = [0.70d, 0.75d, 0.80d, 0.85d, 0.90d, 0.95d];
    private readonly IReadOnlyDictionary<long, FsrsReviewLog[]> reviewLogsByCardId;
    private readonly IReadOnlyList<FsrsReviewLog> reviewLogs;

    internal FsrsOptimizer(IReadOnlyList<FsrsReviewLog> reviewLogs)
    {
        ArgumentNullException.ThrowIfNull(reviewLogs);

        this.reviewLogs = reviewLogs
            .OrderBy(log => log.CardId)
            .ThenBy(log => log.ReviewDatetime)
            .ToArray();
        reviewLogsByCardId = this.reviewLogs
            .GroupBy(log => log.CardId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(log => log.ReviewDatetime)
                    .Take(MaximumSequenceLength)
                    .ToArray());
    }

    internal double[] ComputeOptimalParameters()
    {
        if (CountSameDayFilteredReviews() < MinimumReviewLogCount)
        {
            return FsrsScheduler.DefaultParameters.ToArray();
        }

        double[] bestParameters = FsrsScheduler.DefaultParameters.ToArray();
        double bestLoss = ComputeBatchLoss(bestParameters);
        double[] stepSizes = CreateInitialStepSizes();
        int[] parameterOrder = Enumerable.Range(0, bestParameters.Length).ToArray();
        Random random = new(42);

        for (int epoch = 0; epoch < NumberOfEpochs; epoch++)
        {
            Shuffle(parameterOrder, random);
            foreach (int index in parameterOrder)
            {
                double original = bestParameters[index];
                double bestValue = original;

                foreach (double direction in new[] { -1d, 1d })
                {
                    double candidateValue = ClampParameter(index, original + stepSizes[index] * direction);
                    if (candidateValue.Equals(original))
                    {
                        continue;
                    }

                    double[] candidate = bestParameters.ToArray();
                    candidate[index] = candidateValue;
                    double candidateLoss = ComputeBatchLoss(candidate);
                    if (double.IsFinite(candidateLoss) && candidateLoss < bestLoss)
                    {
                        bestLoss = candidateLoss;
                        bestValue = candidateValue;
                    }
                }

                bestParameters[index] = bestValue;
            }

            for (int i = 0; i < stepSizes.Length; i++)
            {
                stepSizes[i] *= 0.55d;
            }
        }

        return bestParameters;
    }

    internal double ComputeBatchLoss(IReadOnlyList<double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        FsrsScheduler scheduler = new(parameters, enableFuzzing: false);
        double totalLoss = 0;
        int reviewCount = 0;

        foreach (FsrsReviewLog[] history in reviewLogsByCardId.Values)
        {
            if (history.Length == 0)
            {
                continue;
            }

            FsrsCard card = new(cardId: history[0].CardId, due: history[0].ReviewDatetime);
            foreach (FsrsReviewLog reviewLog in history)
            {
                if (card.LastReview is not null
                    && (reviewLog.ReviewDatetime - card.LastReview.Value).Days > 0)
                {
                    double prediction = ClampProbability(
                        scheduler.GetCardRetrievability(card, reviewLog.ReviewDatetime));
                    double target = reviewLog.Rating == FsrsRating.Again ? 0d : 1d;
                    totalLoss += BinaryCrossEntropy(prediction, target);
                    reviewCount++;
                }

                (card, _) = scheduler.ReviewCard(
                    card,
                    reviewLog.Rating,
                    reviewLog.ReviewDatetime,
                    reviewLog.ReviewDuration);
            }
        }

        return reviewCount == 0 ? 0 : totalLoss / reviewCount;
    }

    internal double ComputeOptimalRetention(IReadOnlyList<double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (reviewLogs.Count < MinimumReviewLogCount)
        {
            throw new InvalidOperationException(
                $"At least {MinimumReviewLogCount} review logs are required to compute optimal retention.");
        }

        if (reviewLogs.Any(log => log.ReviewDuration is null))
        {
            throw new InvalidOperationException("Every review log must have a duration to compute optimal retention.");
        }

        OptimizerReviewStatistics statistics = BuildReviewStatistics();
        double bestRetention = RetentionCandidates[0];
        double bestCost = double.PositiveInfinity;

        foreach (double retention in RetentionCandidates)
        {
            double cost = SimulateCost(parameters, retention, statistics);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestRetention = retention;
            }
        }

        return bestRetention;
    }

    private int CountSameDayFilteredReviews()
    {
        int count = 0;
        foreach (FsrsReviewLog[] history in reviewLogsByCardId.Values)
        {
            for (int i = 1; i < history.Length; i++)
            {
                if ((history[i].ReviewDatetime - history[i - 1].ReviewDatetime).Days > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private OptimizerReviewStatistics BuildReviewStatistics()
    {
        FsrsReviewLog[] firstReviews = reviewLogsByCardId.Values
            .Where(history => history.Length > 0)
            .Select(history => history[0])
            .ToArray();
        FsrsReviewLog[] repeatedReviews = reviewLogsByCardId.Values
            .SelectMany(history => history.Skip(1))
            .ToArray();
        FsrsReviewLog[] recallReviews = repeatedReviews
            .Where(log => log.Rating != FsrsRating.Again)
            .ToArray();

        double fallbackCost = AverageDuration(reviewLogs, fallback: 1d);
        return new OptimizerReviewStatistics(
            Probability(firstReviews, FsrsRating.Again),
            Probability(firstReviews, FsrsRating.Hard),
            Probability(firstReviews, FsrsRating.Good),
            Probability(firstReviews, FsrsRating.Easy),
            Probability(recallReviews, FsrsRating.Hard),
            Probability(recallReviews, FsrsRating.Good),
            Probability(recallReviews, FsrsRating.Easy),
            AverageDuration(reviewLogs.Where(log => log.Rating == FsrsRating.Again), fallbackCost),
            AverageDuration(reviewLogs.Where(log => log.Rating == FsrsRating.Hard), fallbackCost),
            AverageDuration(reviewLogs.Where(log => log.Rating == FsrsRating.Good), fallbackCost),
            AverageDuration(reviewLogs.Where(log => log.Rating == FsrsRating.Easy), fallbackCost));
    }

    private static double SimulateCost(
        IReadOnlyList<double> parameters,
        double desiredRetention,
        OptimizerReviewStatistics statistics)
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

    private static FsrsRating ChooseFirstReviewRating(OptimizerReviewStatistics statistics, Random random)
    {
        return ChooseWeighted(
            random,
            (FsrsRating.Again, statistics.FirstAgainProbability),
            (FsrsRating.Hard, statistics.FirstHardProbability),
            (FsrsRating.Good, statistics.FirstGoodProbability),
            (FsrsRating.Easy, statistics.FirstEasyProbability));
    }

    private static FsrsRating ChooseReviewRating(
        OptimizerReviewStatistics statistics,
        double desiredRetention,
        Random random)
    {
        if (random.NextDouble() >= desiredRetention)
        {
            return FsrsRating.Again;
        }

        return ChooseWeighted(
            random,
            (FsrsRating.Hard, statistics.RecallHardProbability),
            (FsrsRating.Good, statistics.RecallGoodProbability),
            (FsrsRating.Easy, statistics.RecallEasyProbability));
    }

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

    private static double[] CreateInitialStepSizes()
    {
        double[] steps = new double[FsrsScheduler.LowerBoundsParameters.Length];
        for (int i = 0; i < steps.Length; i++)
        {
            double range = FsrsScheduler.UpperBoundsParameters[i] - FsrsScheduler.LowerBoundsParameters[i];
            steps[i] = Math.Max(0.001d, range * 0.035d);
        }

        return steps;
    }

    private static double ClampParameter(int index, double value)
    {
        return Math.Clamp(
            value,
            FsrsScheduler.LowerBoundsParameters[index],
            FsrsScheduler.UpperBoundsParameters[index]);
    }

    private static double ClampProbability(double value)
    {
        return Math.Clamp(value, 1e-7d, 1d - 1e-7d);
    }

    private static double BinaryCrossEntropy(double prediction, double target)
    {
        return -(target * Math.Log(prediction) + (1d - target) * Math.Log(1d - prediction));
    }

    private static double Probability(IReadOnlyCollection<FsrsReviewLog> logs, FsrsRating rating)
    {
        return logs.Count == 0
            ? 0
            : logs.Count(log => log.Rating == rating) / (double)logs.Count;
    }

    private static double AverageDuration(IEnumerable<FsrsReviewLog> logs, double fallback)
    {
        int[] durations = logs
            .Select(log => log.ReviewDuration.GetValueOrDefault())
            .Where(duration => duration > 0)
            .ToArray();
        return durations.Length == 0
            ? fallback
            : durations.Average();
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int index = random.Next(i + 1);
            (values[i], values[index]) = (values[index], values[i]);
        }
    }

    private sealed record OptimizerReviewStatistics(
        double FirstAgainProbability,
        double FirstHardProbability,
        double FirstGoodProbability,
        double FirstEasyProbability,
        double RecallHardProbability,
        double RecallGoodProbability,
        double RecallEasyProbability,
        double AgainCost,
        double HardCost,
        double GoodCost,
        double EasyCost)
    {
        internal double CostFor(FsrsRating rating)
        {
            return rating switch
            {
                FsrsRating.Again => AgainCost,
                FsrsRating.Hard => HardCost,
                FsrsRating.Good => GoodCost,
                FsrsRating.Easy => EasyCost,
                _ => GoodCost,
            };
        }
    }
}
