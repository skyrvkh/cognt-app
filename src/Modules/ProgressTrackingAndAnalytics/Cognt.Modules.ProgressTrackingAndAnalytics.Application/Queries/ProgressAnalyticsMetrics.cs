using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsMetrics
{
    internal static int CalculateAverageRecall(IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts)
    {
        int reviewed = 0;
        int retained = 0;
        foreach (ProgressAnalyticsRecallCount count in ratingCounts)
        {
            reviewed += count.Count;
            if (IsRetainedRating(count.Rating))
            {
                retained += count.Count;
            }
        }

        if (reviewed == 0)
        {
            return 0;
        }

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
