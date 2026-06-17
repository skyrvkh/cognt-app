using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

public sealed record ProgressAnalyticsReport(
    ProgressAnalyticsSummaryReadModel Summary,
    IReadOnlyList<DeckPerformanceReadModel> DeckPerformance,
    IReadOnlyList<ProgressAnalyticsActivityDayReadModel> DailyStudyActivity,
    IReadOnlyList<RecallDistributionReadModel> RecallDistribution,
    IReadOnlyList<WeeklyReviewOutcomeReadModel> WeeklyReviewOutcomes,
    IReadOnlyList<ProgressAnalyticsActivityDayReadModel> StudyStreak,
    WorkloadForecastReadModel WorkloadForecast,
    LearningDiagnosticsReadModel LearningDiagnostics);
