namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public sealed record FlashcardWorkloadForecastReadModel(
    IReadOnlyList<FlashcardWorkloadForecastDayReadModel> Days,
    int DueIn7Days,
    int DueIn30Days,
    int DueIn90Days,
    int SuggestedDailyCapacity,
    int SmoothingOpportunityCards,
    IReadOnlyList<FlashcardTomorrowWorkloadContributorReadModel> TomorrowContributors)
{
    public static FlashcardWorkloadForecastReadModel Empty { get; } = new(
        [],
        0,
        0,
        0,
        0,
        0,
        []);
}

public sealed record FlashcardWorkloadForecastDayReadModel(
    DateOnly Date,
    int DueCards,
    double EstimatedMinutes);

public sealed record FlashcardTomorrowWorkloadContributorReadModel(
    string DeckName,
    int CardsScheduledToday);

public sealed record FlashcardLearningDiagnosticsReadModel(
    double? ActualRetention,
    double DesiredRetention,
    int? AverageReviewDurationMilliseconds,
    int TotalLapses,
    int CardsWithLapses,
    int LeechCards,
    IReadOnlyList<FlashcardDistributionBucketReadModel> StabilityDistribution,
    IReadOnlyList<FlashcardDistributionBucketReadModel> DifficultyDistribution)
{
    public static FlashcardLearningDiagnosticsReadModel Empty { get; } = new(
        null,
        FlashcardReviewSettingsReadModel.DefaultDesiredRetention,
        null,
        0,
        0,
        0,
        [],
        []);
}

public sealed record FlashcardDistributionBucketReadModel(
    string Label,
    int Count);
