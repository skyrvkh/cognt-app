using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace CogntApp.ViewModels;

internal static partial class DashboardPresentationBuilder
{
    private static DashboardMetricViewModel[] CreateMetrics(ProgressAnalyticsDashboardReadModel dashboard)
    {
        return
        [
            new DashboardMetricViewModel(
                "Total Cards",
                $"{dashboard.TotalCards:N0}",
                "In this collection",
                string.Empty),
            new DashboardMetricViewModel(
                "Due Cards",
                $"{dashboard.DueCards:N0}",
                dashboard.DueCards == 0 ? "No due cards right now" : "Ready to review",
                string.Empty),
            new DashboardMetricViewModel(
                "New Cards",
                $"{dashboard.NewCards:N0}",
                "Not reviewed yet",
                string.Empty),
            new DashboardMetricViewModel(
                "Reviewed Today",
                $"{dashboard.ReviewedTodayCards:N0}",
                $"Streak: {FormatDays(dashboard.CurrentStreakDays)}",
                $"Best: {FormatDays(dashboard.BestStreakDays)}"),
            new DashboardMetricViewModel(
                "Difficult Cards",
                $"{dashboard.DifficultCards:N0}",
                dashboard.DifficultCards == 0 ? "No difficult cards detected yet" : "Needs attention",
                string.Empty),
        ];
    }

    private static string FormatDays(int days)
    {
        return days == 1 ? "1 day" : $"{days:N0} days";
    }
}
