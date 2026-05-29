using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsRecallCount[] CreateRecallDistribution(
        IReadOnlyList<FlashcardReviewRatingCountReadModel> recallDistribution)
    {
        ProgressAnalyticsRecallCount[] snapshots = new ProgressAnalyticsRecallCount[recallDistribution.Count];
        for (int index = 0; index < recallDistribution.Count; index++)
        {
            FlashcardReviewRatingCountReadModel count = recallDistribution[index];
            snapshots[index] = new ProgressAnalyticsRecallCount(
                SpacedRepetitionSystemRecallRatingMapper.Map(count.Rating),
                count.Count);
        }

        return snapshots;
    }
}
