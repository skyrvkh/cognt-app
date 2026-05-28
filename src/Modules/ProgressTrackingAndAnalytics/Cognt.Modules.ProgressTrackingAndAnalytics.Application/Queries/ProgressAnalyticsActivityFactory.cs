using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsActivityFactory
{
    internal static IReadOnlyDictionary<DateOnly, int> CreateReviewedByDate(
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> reviewActivity)
    {
        Dictionary<DateOnly, int> reviewedByDate = new(reviewActivity.Count);
        foreach (ProgressAnalyticsActivityDayReadModel activity in reviewActivity)
        {
            reviewedByDate.Add(activity.Date, activity.CardsReviewed);
        }

        return reviewedByDate;
    }

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

    internal static DateOnly GetFirstActivityDate(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        return reviewedByDate.Count == 0 ? today : reviewedByDate.Keys.Min();
    }
}
