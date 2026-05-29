using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsRecallDistributionFactory
{
    private static Dictionary<ProgressAnalyticsRecallRating, int> CreateCountsByRating(
        IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts,
        out int total)
    {
        Dictionary<ProgressAnalyticsRecallRating, int> counts = new(ratingCounts.Count);
        total = 0;

        foreach (ProgressAnalyticsRecallCount count in ratingCounts)
        {
            counts.Add(count.Rating, count.Count);
            total += count.Count;
        }

        return counts;
    }
}
