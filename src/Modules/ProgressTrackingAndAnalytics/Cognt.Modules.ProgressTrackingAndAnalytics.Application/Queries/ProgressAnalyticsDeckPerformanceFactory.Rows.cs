using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsDeckPerformanceFactory
{
    private static DeckPerformanceReadModel CreateRow(
        ProgressAnalyticsDeckSummary deck,
        IReadOnlyDictionary<string, ProgressAnalyticsDeckReviewHistory> reviewHistoryByDeck)
    {
        reviewHistoryByDeck.TryGetValue(deck.Name, out ProgressAnalyticsDeckReviewHistory? reviewHistory);
        int reviewed = reviewHistory?.ReviewedCount ?? 0;
        int retained = reviewHistory?.RetainedCount ?? 0;

        return new DeckPerformanceReadModel(
            deck.Name,
            reviewed,
            ProgressAnalyticsMetrics.CalculatePercent(retained, reviewed),
            deck.DueCards,
            deck.ProgressPercent);
    }
}
