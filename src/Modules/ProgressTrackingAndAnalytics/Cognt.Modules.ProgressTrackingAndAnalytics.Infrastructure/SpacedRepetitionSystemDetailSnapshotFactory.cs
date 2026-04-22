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
        return deckReviewHistory
            .Select(history => new ProgressAnalyticsDeckReviewHistory(
                history.DeckName,
                history.ReviewedCount,
                history.RetainedCount))
            .ToArray();
    }

    private static ProgressAnalyticsRecallCount[] CreateRecallDistribution(
        IReadOnlyList<FlashcardReviewRatingCountReadModel> recallDistribution)
    {
        return recallDistribution
            .Select(count => new ProgressAnalyticsRecallCount(
                SpacedRepetitionSystemRecallRatingMapper.Map(count.Rating),
                count.Count))
            .ToArray();
    }

    private static ProgressAnalyticsWeeklyReviewOutcome[] CreateWeeklyReviewOutcomes(
        IReadOnlyList<FlashcardWeeklyReviewOutcomeReadModel> weeklyReviewOutcomes)
    {
        return weeklyReviewOutcomes
            .Select(week => new ProgressAnalyticsWeeklyReviewOutcome(
                week.WeekStart,
                week.WeekEnd,
                week.Again,
                week.Hard,
                week.Good,
                week.Easy))
            .ToArray();
    }
}
