using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsDeckReviewHistory[] CreateDeckReviewHistory(
        IReadOnlyList<FlashcardDeckReviewHistoryReadModel> deckReviewHistory)
    {
        ProgressAnalyticsDeckReviewHistory[] snapshots = new ProgressAnalyticsDeckReviewHistory[deckReviewHistory.Count];
        for (int index = 0; index < deckReviewHistory.Count; index++)
        {
            FlashcardDeckReviewHistoryReadModel history = deckReviewHistory[index];
            snapshots[index] = new ProgressAnalyticsDeckReviewHistory(
                history.DeckName,
                history.ReviewedCount,
                history.RetainedCount);
        }

        return snapshots;
    }
}
