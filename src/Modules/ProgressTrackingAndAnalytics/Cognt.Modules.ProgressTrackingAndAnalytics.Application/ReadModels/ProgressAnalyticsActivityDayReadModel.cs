namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

public sealed record ProgressAnalyticsActivityDayReadModel(
    DateOnly Date,
    int ReviewedCount);
