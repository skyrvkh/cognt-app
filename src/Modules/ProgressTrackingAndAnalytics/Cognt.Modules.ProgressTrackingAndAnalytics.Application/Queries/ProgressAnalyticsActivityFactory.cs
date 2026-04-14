using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsActivityFactory
{
    internal static IReadOnlyDictionary<DateOnly, int> CreateReviewedByDate(
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> reviewActivity)
    {
        return reviewActivity.ToDictionary(activity => activity.Date, activity => activity.CardsReviewed);
    }

    internal static ProgressAnalyticsActivityDayReadModel[] CreateDailyStudyActivity(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate,
        int activityWindowDays)
    {
        return Enumerable
            .Range(0, activityWindowDays)
            .Select(index =>
            {
                DateOnly date = today.AddDays(index - (activityWindowDays - 1));
                reviewedByDate.TryGetValue(date, out int reviewed);
                return new ProgressAnalyticsActivityDayReadModel(date, reviewed);
            })
            .ToArray();
    }

    internal static ProgressAnalyticsActivityDayReadModel[] CreateStudyStreak(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        DateOnly firstDate = GetFirstActivityDate(today, reviewedByDate);
        List<ProgressAnalyticsActivityDayReadModel> rows = [];

        for (DateOnly date = firstDate; date <= today; date = date.AddDays(1))
        {
            reviewedByDate.TryGetValue(date, out int reviewed);
            rows.Add(new ProgressAnalyticsActivityDayReadModel(date, reviewed));
        }

        return [.. rows];
    }

    internal static DateOnly GetFirstActivityDate(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        return reviewedByDate.Count == 0 ? today : reviewedByDate.Keys.Min();
    }
}
