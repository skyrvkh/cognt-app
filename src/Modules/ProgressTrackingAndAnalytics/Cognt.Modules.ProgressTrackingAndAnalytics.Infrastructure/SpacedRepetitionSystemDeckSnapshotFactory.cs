using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemDeckSnapshotFactory
{
    internal static ProgressAnalyticsDeckSummary[] Create(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        return SpacedRepetitionSystemSnapshotRows.CreateDecks(decks);
    }
}
