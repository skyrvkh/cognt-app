using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsRecallDistributionFactory
{
    internal static RecallDistributionReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts)
    {
        Dictionary<ProgressAnalyticsRecallRating, int> counts = ratingCounts
            .ToDictionary(count => count.Rating, count => count.Count);
        int total = counts.Values.Sum();

        return Enum.GetValues<ProgressAnalyticsRecallRating>()
            .Select(rating => CreateRow(rating, counts, total))
            .Where(row => row.Count > 0)
            .ToArray();
    }

    private static RecallDistributionReadModel CreateRow(
        ProgressAnalyticsRecallRating rating,
        IReadOnlyDictionary<ProgressAnalyticsRecallRating, int> counts,
        int total)
    {
        counts.TryGetValue(rating, out int count);
        return new RecallDistributionReadModel(
            rating,
            count,
            ProgressAnalyticsMetrics.CalculatePercent(count, total));
    }
}
