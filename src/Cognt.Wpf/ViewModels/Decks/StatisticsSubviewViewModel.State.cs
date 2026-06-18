using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class StatisticsSubviewViewModel
{
    private void LoadEmptyState()
    {
        LoadReport(
            new ProgressAnalyticsReport(
                new ProgressAnalyticsSummaryReadModel(0, 0, 0, 0, 0, 0),
                [],
                [],
                [],
                [],
                [],
                new WorkloadForecastReadModel([], 0, 0, 0, 0, 0, []),
                new LearningDiagnosticsReadModel(
                    ActualRetention: null,
                    DesiredRetention: 0.9,
                    AverageReviewDurationMilliseconds: null,
                    TotalLapses: 0,
                    CardsWithLapses: 0,
                    LeechCards: 0,
                    StabilityDistribution: [],
                    DifficultyDistribution: [])));
    }

    private void LoadReport(ProgressAnalyticsReport report)
    {
        currentReport = report;

        ApplyPresentationState(
            StatisticsPresentationFactory.Create(
                report,
                DateOnly.FromDateTime(DateTime.Today)));
    }

    private void ApplyPresentationState(StatisticsPresentationState state)
    {
        DeckPerformanceRows = state.DeckPerformanceRows;
        CardsReviewedValue = state.CardsReviewedValue;
        CardsReviewedDetail = state.CardsReviewedDetail;
        StudyStreakValue = state.StudyStreakValue;
        StudyStreakDetail = state.StudyStreakDetail;
        AverageRecallValue = state.AverageRecallValue;
        AverageRecallDetail = state.AverageRecallDetail;
        DueTomorrowValue = state.DueTomorrowValue;
        DueTomorrowDetail = state.DueTomorrowDetail;
        Forecast7Value = state.Forecast7Value;
        Forecast7Detail = state.Forecast7Detail;
        Forecast30Value = state.Forecast30Value;
        Forecast30Detail = state.Forecast30Detail;
        Forecast90Value = state.Forecast90Value;
        Forecast90Detail = state.Forecast90Detail;
        RetentionCalibrationValue = state.RetentionCalibrationValue;
        RetentionCalibrationDetail = state.RetentionCalibrationDetail;
        ReviewLatencyValue = state.ReviewLatencyValue;
        ReviewLatencyDetail = state.ReviewLatencyDetail;
        LapseReportValue = state.LapseReportValue;
        LapseReportDetail = state.LapseReportDetail;
        SmoothingValue = state.SmoothingValue;
        SmoothingDetail = state.SmoothingDetail;
        TomorrowWorkloadDetail = state.TomorrowWorkloadDetail;
        StabilityDistributionLabel = state.StabilityDistributionLabel;
        DifficultyDistributionLabel = state.DifficultyDistributionLabel;
        WorkloadForecastSeries = state.WorkloadForecastSeries;
        WorkloadForecastXAxes = state.WorkloadForecastXAxes;
        WorkloadForecastYAxes = state.WorkloadForecastYAxes;
        DailyStatisticsSeries = state.DailyStatisticsSeries;
        DailyStatisticsXAxes = state.DailyStatisticsXAxes;
        DailyStatisticsYAxes = state.DailyStatisticsYAxes;
        RecallDistributionSeries = state.RecallDistributionSeries;
        StudyStreakSeries = state.StudyStreakSeries;
        StudyStreakXAxes = state.StudyStreakXAxes;
        StudyStreakYAxes = state.StudyStreakYAxes;
        WeeklyOutcomeSeries = state.WeeklyOutcomeSeries;
        WeeklyOutcomeXAxes = state.WeeklyOutcomeXAxes;
        WeeklyOutcomeYAxes = state.WeeklyOutcomeYAxes;
    }
}
