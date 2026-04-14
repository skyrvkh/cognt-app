using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsDashboardFactory
{
    internal static ProgressAnalyticsDashboardReadModel Create(
        ProgressAnalyticsDashboardSnapshot dashboard,
        DateOnly today)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate = ProgressAnalyticsActivityFactory.CreateReviewedByDate(dashboard.ReviewActivity);
        (int currentStreak, int bestStreak) = ProgressAnalyticsStreakCalculator.Calculate(today, reviewedByDate);

        return new ProgressAnalyticsDashboardReadModel(
            dashboard.TotalCards,
            dashboard.DueCards,
            dashboard.NewCards,
            dashboard.ReviewedTodayCards,
            dashboard.DifficultCards,
            dashboard.ReviewHistoryCount,
            currentStreak,
            bestStreak,
            [.. dashboard.ReviewActivity]);
    }
}
