using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal readonly record struct ProgressAnalyticsReviewedActivityShape(
    IReadOnlyDictionary<DateOnly, int> ReviewedByDate,
    int CurrentStreak,
    int BestStreak)
{
    internal static ProgressAnalyticsReviewedActivityShape Create(
        DateOnly today,
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> reviewActivity)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate =
            ProgressAnalyticsActivityFactory.CreateReviewedByDate(reviewActivity);
        (int currentStreak, int bestStreak) = ProgressAnalyticsStreakCalculator.Calculate(today, reviewedByDate);

        return new ProgressAnalyticsReviewedActivityShape(
            reviewedByDate,
            currentStreak,
            bestStreak);
    }
}
