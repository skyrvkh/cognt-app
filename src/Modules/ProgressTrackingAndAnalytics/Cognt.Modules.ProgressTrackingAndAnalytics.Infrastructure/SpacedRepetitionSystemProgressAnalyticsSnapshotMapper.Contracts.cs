using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal interface ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper
{
    CollectionProgressAnalyticsSnapshot CreateCollectionSnapshot(SpacedRepetitionSystemProgressAnalyticsReadData data);
}

internal sealed record SpacedRepetitionSystemProgressAnalyticsReadData(
    IReadOnlyList<FlashcardDeckSummaryReadModel> Decks,
    FlashcardDashboardSummaryReadModel Dashboard,
    FlashcardProgressAnalyticsReadModel Analytics);
