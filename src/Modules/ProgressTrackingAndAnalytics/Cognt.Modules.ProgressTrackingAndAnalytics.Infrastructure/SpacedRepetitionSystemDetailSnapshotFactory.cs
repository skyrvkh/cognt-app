using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemDetailSnapshotFactory
{
    internal static ProgressAnalyticsDetailSnapshot Create(
        FlashcardProgressAnalyticsReadModel analytics)
    {
        return new ProgressAnalyticsDetailSnapshot(
            analytics.DueTomorrowCards,
            analytics.DueTomorrowDecks,
            SpacedRepetitionSystemSnapshotRows.CreateDeckReviewHistory(analytics.DeckReviewHistory),
            SpacedRepetitionSystemSnapshotRows.CreateRecallDistribution(analytics.RecallDistribution),
            SpacedRepetitionSystemSnapshotRows.CreateWeeklyReviewOutcomes(analytics.WeeklyReviewOutcomes));
    }
}
