using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static partial class SpacedRepetitionSystemSnapshotRows
{
    internal static ProgressAnalyticsWorkloadForecast CreateWorkloadForecast(
        FlashcardWorkloadForecastReadModel forecast)
    {
        return new ProgressAnalyticsWorkloadForecast(
            forecast.Days
                .Select(day => new ProgressAnalyticsWorkloadForecastDay(
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
                .Select(contributor => new ProgressAnalyticsTomorrowWorkloadContributor(
                    contributor.DeckName,
                    contributor.CardsScheduledToday))
                .ToArray());
    }

    internal static ProgressAnalyticsLearningDiagnostics CreateLearningDiagnostics(
        FlashcardLearningDiagnosticsReadModel diagnostics)
    {
        return new ProgressAnalyticsLearningDiagnostics(
            diagnostics.ActualRetention,
            diagnostics.DesiredRetention,
            diagnostics.AverageReviewDurationMilliseconds,
            diagnostics.TotalLapses,
            diagnostics.CardsWithLapses,
            diagnostics.LeechCards,
            diagnostics.StabilityDistribution
                .Select(bucket => new ProgressAnalyticsDistributionBucket(bucket.Label, bucket.Count))
                .ToArray(),
            diagnostics.DifficultyDistribution
                .Select(bucket => new ProgressAnalyticsDistributionBucket(bucket.Label, bucket.Count))
                .ToArray());
    }
}
