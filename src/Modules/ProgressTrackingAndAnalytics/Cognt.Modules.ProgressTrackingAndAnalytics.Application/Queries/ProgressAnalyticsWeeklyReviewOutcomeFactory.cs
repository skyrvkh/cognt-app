using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsWeeklyReviewOutcomeFactory
{
    internal static WeeklyReviewOutcomeReadModel[] CreateRows(
        IReadOnlyList<ProgressAnalyticsWeeklyReviewOutcome> weeklyReviewOutcomes)
    {
        WeeklyReviewOutcomeReadModel[] rows = new WeeklyReviewOutcomeReadModel[weeklyReviewOutcomes.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            ProgressAnalyticsWeeklyReviewOutcome week = weeklyReviewOutcomes[index];
            rows[index] = new WeeklyReviewOutcomeReadModel(
                week.WeekStart,
                week.WeekEnd,
                week.Again,
                week.Hard,
                week.Good,
                week.Easy);
        }

        return rows;
    }
}
