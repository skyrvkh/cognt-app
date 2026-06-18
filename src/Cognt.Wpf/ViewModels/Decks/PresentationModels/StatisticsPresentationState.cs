using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace CogntApp.ViewModels.Decks.PresentationModels;

public sealed record StatisticsPresentationState(
    string CardsReviewedValue,
    string CardsReviewedDetail,
    string StudyStreakValue,
    string StudyStreakDetail,
    string AverageRecallValue,
    string AverageRecallDetail,
    string DueTomorrowValue,
    string DueTomorrowDetail,
    IReadOnlyList<DeckPerformanceRow> DeckPerformanceRows,
    ISeries[] DailyStatisticsSeries,
    Axis[] DailyStatisticsXAxes,
    Axis[] DailyStatisticsYAxes,
    ISeries[] RecallDistributionSeries,
    ISeries[] StudyStreakSeries,
    Axis[] StudyStreakXAxes,
    Axis[] StudyStreakYAxes,
    ISeries[] WeeklyOutcomeSeries,
    Axis[] WeeklyOutcomeXAxes,
    Axis[] WeeklyOutcomeYAxes)
{
    public string Forecast7Value { get; init; } = "0";

    public string Forecast7Detail { get; init; } = string.Empty;

    public string Forecast30Value { get; init; } = "0";

    public string Forecast30Detail { get; init; } = string.Empty;

    public string Forecast90Value { get; init; } = "0";

    public string Forecast90Detail { get; init; } = string.Empty;

    public string RetentionCalibrationValue { get; init; } = "—";

    public string RetentionCalibrationDetail { get; init; } = string.Empty;

    public string ReviewLatencyValue { get; init; } = "—";

    public string ReviewLatencyDetail { get; init; } = string.Empty;

    public string LapseReportValue { get; init; } = "0";

    public string LapseReportDetail { get; init; } = string.Empty;

    public string SmoothingValue { get; init; } = "0/day";

    public string SmoothingDetail { get; init; } = string.Empty;

    public string TomorrowWorkloadDetail { get; init; } = string.Empty;

    public string StabilityDistributionLabel { get; init; } = string.Empty;

    public string DifficultyDistributionLabel { get; init; } = string.Empty;

    public ISeries[] WorkloadForecastSeries { get; init; } = [];

    public Axis[] WorkloadForecastXAxes { get; init; } = [];

    public Axis[] WorkloadForecastYAxes { get; init; } = [];
}
