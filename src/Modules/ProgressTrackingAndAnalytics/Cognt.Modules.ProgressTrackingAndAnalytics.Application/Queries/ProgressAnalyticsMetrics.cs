using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsMetrics
{
    internal static int CalculateAverageRecall(IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts)
    {
        int reviewed = ratingCounts.Sum(count => count.Count);
        if (reviewed == 0)
        {
            return 0;
        }

        int retained = ratingCounts
            .Where(count => IsRetainedRating(count.Rating))
            .Sum(count => count.Count);
        return CalculatePercent(retained, reviewed);
    }

    internal static int CalculatePercent(int value, int total)
    {
        return total <= 0 ? 0 : (int)Math.Round(100d * value / total);
    }

    private static bool IsRetainedRating(ProgressAnalyticsRecallRating rating)
    {
        return rating is ProgressAnalyticsRecallRating.Good or ProgressAnalyticsRecallRating.Easy;
    }
}
