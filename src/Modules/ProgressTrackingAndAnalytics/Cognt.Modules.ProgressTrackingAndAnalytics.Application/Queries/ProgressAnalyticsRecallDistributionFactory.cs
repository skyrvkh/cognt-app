using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsRecallDistributionFactory
{
    internal static RecallDistributionReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts)
    {
        Dictionary<ProgressAnalyticsRecallRating, int> counts = new(ratingCounts.Count);
        int total = 0;
        foreach (ProgressAnalyticsRecallCount count in ratingCounts)
        {
            counts.Add(count.Rating, count.Count);
            total += count.Count;
        }

        List<RecallDistributionReadModel> rows = [];
        foreach (ProgressAnalyticsRecallRating rating in Enum.GetValues<ProgressAnalyticsRecallRating>())
        {
            RecallDistributionReadModel row = CreateRow(rating, counts, total);
            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return [.. rows];
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
