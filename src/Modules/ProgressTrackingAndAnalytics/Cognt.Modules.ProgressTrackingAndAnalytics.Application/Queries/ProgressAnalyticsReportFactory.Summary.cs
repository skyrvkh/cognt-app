using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsReportFactory
{
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
