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
    Axis[] WeeklyOutcomeYAxes);
