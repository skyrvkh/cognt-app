using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsReportFactory
{
    private const int ActivityWindowDays = 14;

    internal static ProgressAnalyticsReport Create(
        CollectionProgressAnalyticsSnapshot collectionSnapshot,
        DateOnly today)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate = ProgressAnalyticsActivityFactory.CreateReviewedByDate(collectionSnapshot.Dashboard.ReviewActivity);
        (int currentStreak, int bestStreak) = ProgressAnalyticsStreakCalculator.Calculate(today, reviewedByDate);

        return new ProgressAnalyticsReport(
            CreateSummary(collectionSnapshot, currentStreak, bestStreak),
            ProgressAnalyticsDeckPerformanceFactory.CreateRows(
                collectionSnapshot.Decks,
                collectionSnapshot.Details.DeckReviewHistory),
            ProgressAnalyticsActivityFactory.CreateDailyStudyActivity(today, reviewedByDate, ActivityWindowDays),
            ProgressAnalyticsRecallDistributionFactory.CreateRows(collectionSnapshot.Details.RecallDistribution),
            ProgressAnalyticsWeeklyReviewOutcomeFactory.CreateRows(collectionSnapshot.Details.WeeklyReviewOutcomes),
            ProgressAnalyticsActivityFactory.CreateStudyStreak(today, reviewedByDate));
    }

    private static ProgressAnalyticsSummaryReadModel CreateSummary(
        CollectionProgressAnalyticsSnapshot collectionSnapshot,
        int currentStreak,
        int bestStreak)
    {
        return new ProgressAnalyticsSummaryReadModel(
            collectionSnapshot.Dashboard.ReviewHistoryCount,
            currentStreak,
            bestStreak,
            ProgressAnalyticsMetrics.CalculateAverageRecall(collectionSnapshot.Details.RecallDistribution),
            collectionSnapshot.Details.DueTomorrowCards,
            collectionSnapshot.Details.DueTomorrowDecks);
    }
}
