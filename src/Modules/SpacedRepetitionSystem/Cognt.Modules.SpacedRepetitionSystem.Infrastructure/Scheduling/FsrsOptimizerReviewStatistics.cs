namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed record FsrsOptimizerReviewStatistics(
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
    internal static FsrsOptimizerReviewStatistics Create(
        IReadOnlyList<FsrsReviewLog> reviewLogs,
        IReadOnlyDictionary<long, FsrsReviewLog[]> reviewLogsByCardId)
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
        return new FsrsOptimizerReviewStatistics(
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

    internal double CostFor(FsrsRating rating)
        => rating switch
        {
            FsrsRating.Again => AgainCost,
            FsrsRating.Hard => HardCost,
            FsrsRating.Good => GoodCost,
            FsrsRating.Easy => EasyCost,
            _ => GoodCost,
        };

    private static double Probability(IReadOnlyCollection<FsrsReviewLog> logs, FsrsRating rating)
        => logs.Count == 0
            ? 0
            : logs.Count(log => log.Rating == rating) / (double)logs.Count;

    private static double AverageDuration(IEnumerable<FsrsReviewLog> logs, double fallback)
        => logs
            .Select(log => (double)log.ReviewDuration.GetValueOrDefault())
            .Where(duration => duration > 0)
            .DefaultIfEmpty(fallback)
            .Average();
}
