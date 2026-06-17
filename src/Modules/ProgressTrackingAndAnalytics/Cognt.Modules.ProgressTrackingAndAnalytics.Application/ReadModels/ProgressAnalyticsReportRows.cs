using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

public sealed record ProgressAnalyticsSummaryReadModel(
    int CardsReviewed,
    int CurrentStreakDays,
    int BestStreakDays,
    int AverageRecallPercent,
    int DueTomorrowCards,
    int DueTomorrowDecks);

public sealed record ProgressAnalyticsActivityDayReadModel(
    DateOnly Date,
    int CardsReviewed);

public sealed record DeckPerformanceReadModel(
    string Deck,
    int CardsReviewed,
    int Retention,
    int Due,
    int Progress);

public sealed record RecallDistributionReadModel(
    ProgressAnalyticsRecallRating Rating,
    int Count,
    int Percent);

public sealed record WeeklyReviewOutcomeReadModel(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int Again,
    int Hard,
    int Good,
    int Easy);

public sealed record WorkloadForecastReadModel(
    IReadOnlyList<WorkloadForecastDayReadModel> Days,
    int DueIn7Days,
    int DueIn30Days,
    int DueIn90Days,
    int SuggestedDailyCapacity,
    int SmoothingOpportunityCards,
    IReadOnlyList<TomorrowWorkloadContributorReadModel> TomorrowContributors);

public sealed record WorkloadForecastDayReadModel(
    DateOnly Date,
    int DueCards,
    double EstimatedMinutes);

public sealed record TomorrowWorkloadContributorReadModel(
    string DeckName,
    int CardsScheduledToday);

public sealed record LearningDiagnosticsReadModel(
    double? ActualRetention,
    double DesiredRetention,
    int? AverageReviewDurationMilliseconds,
    int TotalLapses,
    int CardsWithLapses,
    int LeechCards,
    IReadOnlyList<DistributionBucketReadModel> StabilityDistribution,
    IReadOnlyList<DistributionBucketReadModel> DifficultyDistribution);

public sealed record DistributionBucketReadModel(
    string Label,
    int Count);
