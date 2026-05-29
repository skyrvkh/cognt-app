using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsActivityDayReadModel[] CreateReviewActivity(
        IReadOnlyList<FlashcardReviewActivityReadModel> reviewActivity)
    {
        ProgressAnalyticsActivityDayReadModel[] snapshots = new ProgressAnalyticsActivityDayReadModel[reviewActivity.Count];
        for (int index = 0; index < reviewActivity.Count; index++)
        {
            FlashcardReviewActivityReadModel activity = reviewActivity[index];
            snapshots[index] = new ProgressAnalyticsActivityDayReadModel(
                activity.Date,
                activity.ReviewCount);
        }

        return snapshots;
    }
}
