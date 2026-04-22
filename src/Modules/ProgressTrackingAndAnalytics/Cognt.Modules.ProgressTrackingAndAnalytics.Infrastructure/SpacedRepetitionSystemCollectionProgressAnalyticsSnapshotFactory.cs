using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemCollectionProgressAnalyticsSnapshotFactory
{
    internal static CollectionProgressAnalyticsSnapshot CreateCollectionSnapshot(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        FlashcardDashboardSummaryReadModel dashboard,
        FlashcardProgressAnalyticsReadModel analytics)
    {
        return new CollectionProgressAnalyticsSnapshot(
            SpacedRepetitionSystemDeckSnapshotFactory.Create(decks),
            SpacedRepetitionSystemDashboardSnapshotFactory.Create(dashboard),
            SpacedRepetitionSystemDetailSnapshotFactory.Create(analytics));
    }
}
