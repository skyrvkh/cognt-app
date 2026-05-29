using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsWeeklyReviewOutcome[] CreateWeeklyReviewOutcomes(
        IReadOnlyList<FlashcardWeeklyReviewOutcomeReadModel> weeklyReviewOutcomes)
    {
        ProgressAnalyticsWeeklyReviewOutcome[] snapshots = new ProgressAnalyticsWeeklyReviewOutcome[weeklyReviewOutcomes.Count];
        for (int index = 0; index < weeklyReviewOutcomes.Count; index++)
        {
            FlashcardWeeklyReviewOutcomeReadModel week = weeklyReviewOutcomes[index];
            snapshots[index] = new ProgressAnalyticsWeeklyReviewOutcome(
                week.WeekStart,
                week.WeekEnd,
                week.Again,
                week.Hard,
                week.Good,
                week.Easy);
        }

        return snapshots;
    }
}
