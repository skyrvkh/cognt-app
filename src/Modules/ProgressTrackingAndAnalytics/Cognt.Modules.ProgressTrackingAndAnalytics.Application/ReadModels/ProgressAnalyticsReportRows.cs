using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

public sealed record ProgressAnalyticsSummaryReadModel(
    int CardsReviewed,
    int CurrentStreakDays,
    int BestStreakDays,
    int AverageRecallPercent,
    int DueTomorrowCards,
    int DueTomorrowDecks);

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
