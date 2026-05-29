using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal sealed class SpacedRepetitionSystemProgressAnalyticsSnapshotMapper : ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper
{
    public CollectionProgressAnalyticsSnapshot CreateCollectionSnapshot(SpacedRepetitionSystemProgressAnalyticsReadData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return SpacedRepetitionSystemCollectionProgressAnalyticsSnapshotFactory.CreateCollectionSnapshot(
            data.Decks,
            data.Dashboard,
            data.Analytics);
    }
}
