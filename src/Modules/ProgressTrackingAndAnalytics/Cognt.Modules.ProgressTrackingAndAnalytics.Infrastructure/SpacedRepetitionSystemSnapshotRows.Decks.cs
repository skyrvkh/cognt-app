using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsDeckSummary[] CreateDecks(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        ProgressAnalyticsDeckSummary[] snapshots = new ProgressAnalyticsDeckSummary[decks.Count];
        for (int index = 0; index < decks.Count; index++)
        {
            FlashcardDeckSummaryReadModel deck = decks[index];
            snapshots[index] = new ProgressAnalyticsDeckSummary(
                deck.Name,
                deck.DueCards,
                deck.ProgressPercent);
        }

        return snapshots;
    }
}
