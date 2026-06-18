using System.Globalization;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using CogntApp.Design;
using LiveChartsCore;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class StatisticsPresentationFactory
{
    private static StatisticsPresentationState ApplyLearningDiagnostics(
        StatisticsPresentationState state,
        ProgressAnalyticsReport report)
    {
        WorkloadForecastReadModel forecast = report.WorkloadForecast;
        LearningDiagnosticsReadModel diagnostics = report.LearningDiagnostics;
        WorkloadForecastDayReadModel[] chartDays = forecast.Days.Take(30).ToArray();
        double estimatedMinutesPerDay = forecast.Days.Take(7).Any()
            ? forecast.Days.Take(7).Average(day => day.EstimatedMinutes)
            : 0d;
        string contributors = FormatTomorrowContributors(forecast.TomorrowContributors);

        return state with
        {
            Forecast7Value = forecast.DueIn7Days.ToString("N0", CultureInfo.InvariantCulture),
            Forecast7Detail = diagnostics.AverageReviewDurationMilliseconds.HasValue
                ? string.Concat("~", FormatMinutes(estimatedMinutesPerDay), " per day")
                : string.Concat("~", FormatMinutes(estimatedMinutesPerDay), " per day · default timing"),
            Forecast30Value = forecast.DueIn30Days.ToString("N0", CultureInfo.InvariantCulture),
            Forecast30Detail = "Scheduled due cards",
            Forecast90Value = forecast.DueIn90Days.ToString("N0", CultureInfo.InvariantCulture),
            Forecast90Detail = "Scheduled due cards",
            RetentionCalibrationValue = diagnostics.ActualRetention.HasValue
                ? FormatPercent(diagnostics.ActualRetention.Value)
                : "—",
            RetentionCalibrationDetail = string.Concat(
                "Target ",
                FormatPercent(diagnostics.DesiredRetention),
                diagnostics.ActualRetention.HasValue ? " · Good/Easy" : " · awaiting reviews"),
            ReviewLatencyValue = FormatReviewLatency(diagnostics.AverageReviewDurationMilliseconds),
            ReviewLatencyDetail = diagnostics.AverageReviewDurationMilliseconds.HasValue
                ? "Average answer time"
                : "Timing begins with new reviews",
            LapseReportValue = diagnostics.TotalLapses.ToString("N0", CultureInfo.InvariantCulture),
            LapseReportDetail = string.Concat(
                diagnostics.CardsWithLapses.ToString("N0", CultureInfo.InvariantCulture),
                " cards lapsed · ",
                diagnostics.LeechCards.ToString("N0", CultureInfo.InvariantCulture),
                " leeches (8+)"),
            SmoothingValue = string.Concat(
                forecast.SuggestedDailyCapacity.ToString("N0", CultureInfo.InvariantCulture),
                "/day"),
            SmoothingDetail = forecast.SmoothingOpportunityCards == 0
                ? "Next 7 days are balanced"
                : string.Concat(
                    forecast.SmoothingOpportunityCards.ToString("N0", CultureInfo.InvariantCulture),
                    " cards above the 7-day target"),
            TomorrowWorkloadDetail = contributors,
            StabilityDistributionLabel = FormatDistribution(diagnostics.StabilityDistribution),
            DifficultyDistributionLabel = FormatDistribution(diagnostics.DifficultyDistribution),
            WorkloadForecastSeries =
            [
                CreateColumnSeries(
                    "Scheduled due",
                    chartDays.Select(day => day.DueCards).ToArray(),
                    ThemeResourceLookup.ColorHex("AccentBrush", "#2563EB"),
                    10),
            ],
            WorkloadForecastXAxes = [CreateCategoryAxis(chartDays.Select(day => FormatShortDate(day.Date)).ToArray())],
            WorkloadForecastYAxes = [CreateValueAxis()],
        };
    }

    private static string FormatReviewLatency(int? reviewDurationMilliseconds)
    {
        if (!reviewDurationMilliseconds.HasValue)
        {
            return "—";
        }

        return reviewDurationMilliseconds.Value >= 60_000
            ? string.Concat((reviewDurationMilliseconds.Value / 60_000d).ToString("0.0", CultureInfo.InvariantCulture), " min")
            : string.Concat((reviewDurationMilliseconds.Value / 1_000d).ToString("0.0", CultureInfo.InvariantCulture), " sec");
    }

    private static string FormatMinutes(double minutes)
    {
        return minutes < 1d
            ? "< 1 min"
            : string.Concat(minutes.ToString("0.0", CultureInfo.InvariantCulture), " min");
    }

    private static string FormatPercent(double value)
    {
        return string.Concat(Math.Round(value * 100d).ToString("N0", CultureInfo.InvariantCulture), "%");
    }

    private static string FormatTomorrowContributors(
        IReadOnlyList<TomorrowWorkloadContributorReadModel> contributors)
    {
        if (contributors.Count == 0)
        {
            return "No cards reviewed today are scheduled for tomorrow.";
        }

        return string.Concat(
            "Tomorrow created today: ",
            string.Join(
                " · ",
                contributors.Take(3).Select(contributor => string.Concat(
                    contributor.DeckName,
                    " ",
                    contributor.CardsScheduledToday.ToString("N0", CultureInfo.InvariantCulture)))));
    }

    private static string FormatDistribution(
        IReadOnlyList<DistributionBucketReadModel> distribution)
    {
        string[] labels = distribution
            .Where(bucket => bucket.Count > 0)
            .Select(bucket => string.Concat(bucket.Label, ": ", bucket.Count.ToString("N0", CultureInfo.InvariantCulture)))
            .ToArray();
        return labels.Length == 0 ? "No cards in this scope." : string.Join(" · ", labels);
    }
}
