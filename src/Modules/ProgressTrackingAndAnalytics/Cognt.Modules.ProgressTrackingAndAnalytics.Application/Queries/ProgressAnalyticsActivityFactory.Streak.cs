using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsActivityFactory
{
    internal static ProgressAnalyticsActivityDayReadModel[] CreateStudyStreak(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        DateOnly firstDate = GetFirstActivityDate(today, reviewedByDate);
        if (firstDate > today)
        {
            return [];
        }

        int dayCount = today.DayNumber - firstDate.DayNumber + 1;
        ProgressAnalyticsActivityDayReadModel[] rows = new ProgressAnalyticsActivityDayReadModel[dayCount];
        for (int index = 0; index < rows.Length; index++)
        {
            DateOnly date = firstDate.AddDays(index);
            reviewedByDate.TryGetValue(date, out int reviewed);
            rows[index] = new ProgressAnalyticsActivityDayReadModel(date, reviewed);
        }

        return rows;
    }
}
