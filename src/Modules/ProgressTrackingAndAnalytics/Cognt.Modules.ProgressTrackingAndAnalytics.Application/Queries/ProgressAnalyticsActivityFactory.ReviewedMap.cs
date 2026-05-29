using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsActivityFactory
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

    internal static DateOnly GetFirstActivityDate(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        return reviewedByDate.Count == 0 ? today : reviewedByDate.Keys.Min();
    }
}
