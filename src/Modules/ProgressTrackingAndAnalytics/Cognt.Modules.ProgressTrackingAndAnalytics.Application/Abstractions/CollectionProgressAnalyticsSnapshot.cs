using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;

public sealed record CollectionProgressAnalyticsSnapshot(
    IReadOnlyList<ProgressAnalyticsDeckSummary> Decks,
    ProgressAnalyticsDashboardSnapshot Dashboard,
    ProgressAnalyticsDetailSnapshot Details);

public sealed record ProgressAnalyticsDeckSummary(
    string Name,
    int DueCards,
    int ProgressPercent);

public sealed record ProgressAnalyticsDashboardSnapshot(
    int TotalCards,
    int DueCards,
    int NewCards,
    int ReviewedTodayCards,
    int DifficultCards,
    int ReviewHistoryCount,
    IReadOnlyList<ProgressAnalyticsActivityDayReadModel> ReviewActivity);

public sealed record ProgressAnalyticsDetailSnapshot(
    int DueTomorrowCards,
    int DueTomorrowDecks,
    IReadOnlyList<ProgressAnalyticsDeckReviewHistory> DeckReviewHistory,
    IReadOnlyList<ProgressAnalyticsRecallCount> RecallDistribution,
    IReadOnlyList<ProgressAnalyticsWeeklyReviewOutcome> WeeklyReviewOutcomes,
    ProgressAnalyticsWorkloadForecast WorkloadForecast,
    ProgressAnalyticsLearningDiagnostics LearningDiagnostics);

public sealed record ProgressAnalyticsDeckReviewHistory(
    string DeckName,
    int ReviewedCount,
    int RetainedCount);

public sealed record ProgressAnalyticsRecallCount(
    ProgressAnalyticsRecallRating Rating,
    int Count);

public sealed record ProgressAnalyticsWeeklyReviewOutcome(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int Again,
    int Hard,
    int Good,
    int Easy);

public sealed record ProgressAnalyticsWorkloadForecast(
    IReadOnlyList<ProgressAnalyticsWorkloadForecastDay> Days,
    int DueIn7Days,
    int DueIn30Days,
    int DueIn90Days,
    int SuggestedDailyCapacity,
    int SmoothingOpportunityCards,
    IReadOnlyList<ProgressAnalyticsTomorrowWorkloadContributor> TomorrowContributors);

public sealed record ProgressAnalyticsWorkloadForecastDay(
    DateOnly Date,
    int DueCards,
    double EstimatedMinutes);

public sealed record ProgressAnalyticsTomorrowWorkloadContributor(
    string DeckName,
    int CardsScheduledToday);

public sealed record ProgressAnalyticsLearningDiagnostics(
    double? ActualRetention,
    double DesiredRetention,
    int? AverageReviewDurationMilliseconds,
    int TotalLapses,
    int CardsWithLapses,
    int LeechCards,
    IReadOnlyList<ProgressAnalyticsDistributionBucket> StabilityDistribution,
    IReadOnlyList<ProgressAnalyticsDistributionBucket> DifficultyDistribution);

public sealed record ProgressAnalyticsDistributionBucket(
    string Label,
    int Count);
