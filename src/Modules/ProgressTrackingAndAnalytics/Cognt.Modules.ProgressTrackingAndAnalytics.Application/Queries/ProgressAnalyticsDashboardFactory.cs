using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsDashboardFactory
{
    internal static ProgressAnalyticsDashboardReadModel Create(
        ProgressAnalyticsDashboardSnapshot dashboard,
        DateOnly today)
    {
        ProgressAnalyticsReviewedActivityShape reviewedActivity =
            ProgressAnalyticsReviewedActivityShape.Create(today, dashboard.ReviewActivity);

        return new ProgressAnalyticsDashboardReadModel(
            dashboard.TotalCards,
            dashboard.DueCards,
            dashboard.NewCards,
            dashboard.ReviewedTodayCards,
            dashboard.DifficultCards,
            dashboard.ReviewHistoryCount,
            reviewedActivity.CurrentStreak,
            reviewedActivity.BestStreak,
            [.. dashboard.ReviewActivity]);
    }
}
