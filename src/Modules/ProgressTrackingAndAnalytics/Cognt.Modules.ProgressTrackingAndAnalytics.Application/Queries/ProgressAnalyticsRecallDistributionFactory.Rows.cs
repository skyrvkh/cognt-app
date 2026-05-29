using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsRecallDistributionFactory
{
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
