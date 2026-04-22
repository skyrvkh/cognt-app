using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemDashboardSnapshotFactory
{
    internal static ProgressAnalyticsDashboardSnapshot Create(
        FlashcardDashboardSummaryReadModel dashboard)
    {
        return new ProgressAnalyticsDashboardSnapshot(
            dashboard.TotalCards,
            dashboard.DueCards,
            dashboard.NewCards,
            dashboard.ReviewedTodayCards,
            dashboard.DifficultCards,
            dashboard.ReviewHistoryCount,
            dashboard.ReviewActivity
                .Select(activity => new ProgressAnalyticsActivityDayReadModel(
                    activity.Date,
                    activity.ReviewCount))
                .ToArray());
    }
}
