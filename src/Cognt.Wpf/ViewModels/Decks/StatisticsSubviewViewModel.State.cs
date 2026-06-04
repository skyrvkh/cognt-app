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
                []));
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
