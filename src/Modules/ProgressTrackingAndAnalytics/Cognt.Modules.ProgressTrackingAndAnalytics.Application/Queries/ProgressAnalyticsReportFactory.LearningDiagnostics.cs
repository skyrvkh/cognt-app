using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsReportFactory
{
    private static WorkloadForecastReadModel CreateWorkloadForecast(
        ProgressAnalyticsWorkloadForecast forecast)
    {
        return new WorkloadForecastReadModel(
            forecast.Days
                .Select(day => new WorkloadForecastDayReadModel(
                    day.Date,
                    day.DueCards,
                    day.EstimatedMinutes))
                .ToArray(),
            forecast.DueIn7Days,
            forecast.DueIn30Days,
            forecast.DueIn90Days,
            forecast.SuggestedDailyCapacity,
            forecast.SmoothingOpportunityCards,
            forecast.TomorrowContributors
                .Select(contributor => new TomorrowWorkloadContributorReadModel(
                    contributor.DeckName,
                    contributor.CardsScheduledToday))
                .ToArray());
    }

    private static LearningDiagnosticsReadModel CreateLearningDiagnostics(
        ProgressAnalyticsLearningDiagnostics diagnostics)
    {
        return new LearningDiagnosticsReadModel(
            diagnostics.ActualRetention,
            diagnostics.DesiredRetention,
            diagnostics.AverageReviewDurationMilliseconds,
            diagnostics.TotalLapses,
            diagnostics.CardsWithLapses,
            diagnostics.LeechCards,
            diagnostics.StabilityDistribution
                .Select(bucket => new DistributionBucketReadModel(bucket.Label, bucket.Count))
                .ToArray(),
            diagnostics.DifficultyDistribution
                .Select(bucket => new DistributionBucketReadModel(bucket.Label, bucket.Count))
                .ToArray());
    }
}
