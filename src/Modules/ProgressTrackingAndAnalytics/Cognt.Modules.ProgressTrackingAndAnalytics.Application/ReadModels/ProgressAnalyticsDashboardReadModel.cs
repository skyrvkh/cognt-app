namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

public sealed record ProgressAnalyticsDashboardReadModel(
    int TotalCards,
    int DueCards,
    int NewCards,
    int ReviewedTodayCards,
    int DifficultCards,
    int ReviewHistoryCount,
    int CurrentStreakDays,
    int BestStreakDays,
    IReadOnlyList<ProgressAnalyticsActivityDayReadModel> ReviewActivity);
