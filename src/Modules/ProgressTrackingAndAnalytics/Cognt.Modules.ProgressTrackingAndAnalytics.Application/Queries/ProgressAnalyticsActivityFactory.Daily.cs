using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsActivityFactory
{
    internal static ProgressAnalyticsActivityDayReadModel[] CreateDailyStudyActivity(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate,
        int activityWindowDays)
    {
        ProgressAnalyticsActivityDayReadModel[] rows = new ProgressAnalyticsActivityDayReadModel[activityWindowDays];
        for (int index = 0; index < rows.Length; index++)
        {
            DateOnly date = today.AddDays(index - (activityWindowDays - 1));
            reviewedByDate.TryGetValue(date, out int reviewed);
            rows[index] = new ProgressAnalyticsActivityDayReadModel(date, reviewed);
        }

        return rows;
    }
}
