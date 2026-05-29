using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsRecallDistributionFactory
{
    internal static RecallDistributionReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsRecallCount> ratingCounts)
    {
        Dictionary<ProgressAnalyticsRecallRating, int> counts = CreateCountsByRating(ratingCounts, out int total);

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
}
