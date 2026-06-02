using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace CogntApp.ViewModels;

internal static partial class DashboardPresentationBuilder
{
    private static DashboardStudyHeatmapCellViewModel[] CreateHeatmap(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        const int weekCount = 53;
        const int daysPerWeek = 7;
        const int heatmapCellCount = weekCount * daysPerWeek;
        DateOnly firstWeekStart = GetWeekStart(today).AddDays(-(weekCount - 1) * daysPerWeek);

        DashboardStudyHeatmapCellViewModel[] cells = new DashboardStudyHeatmapCellViewModel[heatmapCellCount];
        for (int dayIndex = 0; dayIndex < daysPerWeek; dayIndex++)
        {
            for (int weekIndex = 0; weekIndex < weekCount; weekIndex++)
            {
                DateOnly date = firstWeekStart.AddDays((weekIndex * daysPerWeek) + dayIndex);
                reviewedByDate.TryGetValue(date, out int reviewed);
                cells[(dayIndex * weekCount) + weekIndex] =
                    new DashboardStudyHeatmapCellViewModel(date, reviewed, HeatmapLevel(reviewed));
            }
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
}
