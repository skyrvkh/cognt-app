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
            CreateDeckReviewHistory(analytics.DeckReviewHistory),
            CreateRecallDistribution(analytics.RecallDistribution),
            CreateWeeklyReviewOutcomes(analytics.WeeklyReviewOutcomes));
    }

    private static ProgressAnalyticsDeckReviewHistory[] CreateDeckReviewHistory(
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

    private static ProgressAnalyticsRecallCount[] CreateRecallDistribution(
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

    private static ProgressAnalyticsWeeklyReviewOutcome[] CreateWeeklyReviewOutcomes(
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
