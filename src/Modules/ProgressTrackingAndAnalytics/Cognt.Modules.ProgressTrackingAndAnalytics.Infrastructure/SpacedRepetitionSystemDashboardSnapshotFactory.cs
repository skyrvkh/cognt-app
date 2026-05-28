using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemDashboardSnapshotFactory
{
    internal static ProgressAnalyticsDashboardSnapshot Create(
        FlashcardDashboardSummaryReadModel dashboard)
    {
        ProgressAnalyticsActivityDayReadModel[] reviewActivity = CreateReviewActivity(dashboard.ReviewActivity);

        return new ProgressAnalyticsDashboardSnapshot(
            dashboard.TotalCards,
            dashboard.DueCards,
            dashboard.NewCards,
            dashboard.ReviewedTodayCards,
            dashboard.DifficultCards,
            dashboard.ReviewHistoryCount,
            reviewActivity);
    }

    private static ProgressAnalyticsActivityDayReadModel[] CreateReviewActivity(
        IReadOnlyList<FlashcardReviewActivityReadModel> reviewActivity)
    {
        ProgressAnalyticsActivityDayReadModel[] snapshots = new ProgressAnalyticsActivityDayReadModel[reviewActivity.Count];
        for (int index = 0; index < reviewActivity.Count; index++)
        {
            FlashcardReviewActivityReadModel activity = reviewActivity[index];
            snapshots[index] = new ProgressAnalyticsActivityDayReadModel(
                activity.Date,
                activity.ReviewCount);
        }

        return snapshots;
    }
}
