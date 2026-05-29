namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed class FsrsOptimizer
{
    private const int MinimumReviewLogCount = 512;
    private const int MaximumSequenceLength = 64;
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
        return FsrsOptimizerParameterSearch.Compute(
            CountSameDayFilteredReviews(),
            MinimumReviewLogCount,
            ComputeBatchLoss);
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

        return FsrsOptimizerRetentionSearch.Compute(
            parameters,
            reviewLogs,
            reviewLogsByCardId,
            MinimumReviewLogCount);
    }

    private int CountSameDayFilteredReviews()
        => reviewLogsByCardId.Values.Sum(history => history
            .Zip(history.Skip(1), static (previous, current) => (previous, current))
            .Count(pair => (pair.current.ReviewDatetime - pair.previous.ReviewDatetime).Days > 0));

    private static double ClampProbability(double value)
        => Math.Clamp(value, 1e-7d, 1d - 1e-7d);

    private static double BinaryCrossEntropy(double prediction, double target)
        => -(target * Math.Log(prediction) + (1d - target) * Math.Log(1d - prediction));

}
