using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsWeeklyReviewOutcomeFactory
{
    internal static WeeklyReviewOutcomeReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsWeeklyReviewOutcome> weeklyReviewOutcomes)
    {
        return weeklyReviewOutcomes
            .Select(week => new WeeklyReviewOutcomeReadModel(
                week.WeekStart,
                week.WeekEnd,
                week.Again,
                week.Hard,
                week.Good,
                week.Easy))
            .ToArray();
    }
}
