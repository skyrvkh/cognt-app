using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsDeckPerformanceFactory
{
    internal static DeckPerformanceReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsDeckSummary> decks,
        IReadOnlyList<ProgressAnalyticsDeckReviewHistory> deckReviewHistory)
    {
        Dictionary<string, ProgressAnalyticsDeckReviewHistory> reviewHistoryByDeck =
            CreateReviewHistoryByDeck(deckReviewHistory);

        DeckPerformanceReadModel[] rows = new DeckPerformanceReadModel[decks.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = CreateRow(decks[index], reviewHistoryByDeck);
        }

        return rows;
    }
}
