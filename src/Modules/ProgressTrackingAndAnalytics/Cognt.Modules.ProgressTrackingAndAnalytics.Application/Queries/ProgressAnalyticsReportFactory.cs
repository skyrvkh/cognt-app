using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsReportFactory
{
    private const int ActivityWindowDays = 14;

    internal static ProgressAnalyticsReport Create(
        CollectionProgressAnalyticsSnapshot collectionSnapshot,
        DateOnly today)
    {
        ProgressAnalyticsReviewedActivityShape reviewedActivity =
            ProgressAnalyticsReviewedActivityShape.Create(today, collectionSnapshot.Dashboard.ReviewActivity);

        return new ProgressAnalyticsReport(
            CreateSummary(collectionSnapshot, reviewedActivity.CurrentStreak, reviewedActivity.BestStreak),
            ProgressAnalyticsDeckPerformanceFactory.CreateRows(
                collectionSnapshot.Decks,
                collectionSnapshot.Details.DeckReviewHistory),
            ProgressAnalyticsActivityFactory.CreateDailyStudyActivity(
                today,
                reviewedActivity.ReviewedByDate,
                ActivityWindowDays),
            ProgressAnalyticsRecallDistributionFactory.CreateRows(collectionSnapshot.Details.RecallDistribution),
            ProgressAnalyticsWeeklyReviewOutcomeFactory.CreateRows(collectionSnapshot.Details.WeeklyReviewOutcomes),
            ProgressAnalyticsActivityFactory.CreateStudyStreak(today, reviewedActivity.ReviewedByDate));
    }

}
