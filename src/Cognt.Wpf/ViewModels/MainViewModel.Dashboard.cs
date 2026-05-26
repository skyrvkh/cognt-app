using System.IO;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void ShowDashboard()
    {
        SaveCurrentNoteFromEditor();
        RefreshDashboardData();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Dashboard));
    }

    private void RefreshDashboardData()
    {
        DashboardMetrics.Clear();
        DashboardStudyHeatmapCells.Clear();
        DashboardEmptyStates.Clear();

        ProgressAnalyticsDashboardReadModel dashboard = new(0, 0, 0, 0, 0, 0, 0, 0, []);

        if (!string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath))
        {
            try
            {
                dashboard = collectionProgressAnalyticsDashboardQuery.Execute(
                    new CollectionProgressAnalyticsDashboardQueryRequest(
                        openCollectionPath,
                        DateOnly.FromDateTime(DateTime.Today)));
            }
            catch (Exception ex) when (IsRecoverableFileSystemException(ex))
            {
                dashboard = new ProgressAnalyticsDashboardReadModel(0, 0, 0, 0, 0, 0, 0, 0, []);
            }
        }

        IReadOnlyDictionary<DateOnly, int> reviewedByDate = CreateReviewedByDateMap(dashboard.ReviewActivity);

        if (dashboard.TotalCards == 0)
        {
            DashboardEmptyStates.Add(new DashboardEmptyStateViewModel(
                "No cards yet",
                "Create cards from your notes to start reviewing this collection."));
        }
        else if (dashboard.ReviewHistoryCount == 0)
        {
            DashboardEmptyStates.Add(new DashboardEmptyStateViewModel(
                "Insufficient review data",
                "Review a few due cards to build activity, recall, and streak data."));
        }

        DashboardMetrics.Add(new DashboardMetricViewModel(
            "Total Cards",
            $"{dashboard.TotalCards:N0}",
            "In this collection",
            string.Empty));
        DashboardMetrics.Add(new DashboardMetricViewModel(
            "Due Cards",
            $"{dashboard.DueCards:N0}",
            dashboard.DueCards == 0 ? "No due cards right now" : "Ready to review",
            string.Empty));
        DashboardMetrics.Add(new DashboardMetricViewModel(
            "New Cards",
            $"{dashboard.NewCards:N0}",
            "Not reviewed yet",
            string.Empty));
        DashboardMetrics.Add(new DashboardMetricViewModel(
            "Reviewed Today",
            $"{dashboard.ReviewedTodayCards:N0}",
            $"Streak: {FormatDays(dashboard.CurrentStreakDays)}",
            $"Best: {FormatDays(dashboard.BestStreakDays)}"));
        DashboardMetrics.Add(new DashboardMetricViewModel(
            "Difficult Cards",
            $"{dashboard.DifficultCards:N0}",
            dashboard.DifficultCards == 0 ? "No difficult cards detected yet" : "Needs attention",
            string.Empty));

        foreach (DashboardStudyHeatmapCellViewModel cell in CreateDashboardHeatmap(
                     DateOnly.FromDateTime(DateTime.Today),
                     reviewedByDate))
        {
            DashboardStudyHeatmapCells.Add(cell);
        }
    }

    private static DashboardStudyHeatmapCellViewModel[] CreateDashboardHeatmap(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        const int weekCount = 52;
        const int daysPerWeek = 7;
        const int heatmapCellCount = weekCount * daysPerWeek;
        DateOnly firstDate = GetWeekStart(today).AddDays(-(weekCount - 1) * daysPerWeek);

        DashboardStudyHeatmapCellViewModel[] cells = new DashboardStudyHeatmapCellViewModel[heatmapCellCount];
        for (int offset = 0; offset < heatmapCellCount; offset++)
        {
            DateOnly date = firstDate.AddDays(offset);
            reviewedByDate.TryGetValue(date, out int reviewed);
            cells[offset] = new DashboardStudyHeatmapCellViewModel(date, reviewed, HeatmapLevel(reviewed));
        }

        return cells;
    }

    private static IReadOnlyDictionary<DateOnly, int> CreateReviewedByDateMap(
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> reviewActivity)
    {
        Dictionary<DateOnly, int> reviewedByDate = new(reviewActivity.Count);
        foreach (ProgressAnalyticsActivityDayReadModel activity in reviewActivity)
        {
            reviewedByDate.Add(activity.Date, activity.CardsReviewed);
        }

        return reviewedByDate;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        int dayOffset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-dayOffset);
    }

    private static int HeatmapLevel(int reviews)
    {
        return reviews switch
        {
            0 => 0,
            < 3 => 1,
            < 8 => 2,
            < 15 => 3,
            _ => 4,
        };
    }

    private static string FormatDays(int days)
    {
        return days == 1 ? "1 day" : $"{days:N0} days";
    }
}
