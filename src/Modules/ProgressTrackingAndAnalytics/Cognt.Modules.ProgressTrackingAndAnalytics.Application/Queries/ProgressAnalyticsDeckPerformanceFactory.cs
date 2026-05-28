using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsDeckPerformanceFactory
{
    internal static DeckPerformanceReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsDeckSummary> decks,
        IReadOnlyList<ProgressAnalyticsDeckReviewHistory> deckReviewHistory)
    {
        Dictionary<string, ProgressAnalyticsDeckReviewHistory> reviewHistoryByDeck = new(
            deckReviewHistory.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (ProgressAnalyticsDeckReviewHistory deck in deckReviewHistory)
        {
            reviewHistoryByDeck.Add(deck.DeckName, deck);
        }

        DeckPerformanceReadModel[] rows = new DeckPerformanceReadModel[decks.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = CreateRow(decks[index], reviewHistoryByDeck);
        }

        return rows;
    }

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
