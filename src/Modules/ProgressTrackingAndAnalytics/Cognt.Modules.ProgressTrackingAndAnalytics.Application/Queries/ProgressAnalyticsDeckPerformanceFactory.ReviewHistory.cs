using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsDeckPerformanceFactory
{
    private static Dictionary<string, ProgressAnalyticsDeckReviewHistory> CreateReviewHistoryByDeck(
        IReadOnlyList<ProgressAnalyticsDeckReviewHistory> deckReviewHistory)
    {
        Dictionary<string, ProgressAnalyticsDeckReviewHistory> reviewHistoryByDeck = new(
            deckReviewHistory.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (ProgressAnalyticsDeckReviewHistory deck in deckReviewHistory)
        {
            reviewHistoryByDeck.Add(deck.DeckName, deck);
        }

        return reviewHistoryByDeck;
    }
}
