namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal enum FlashcardReviewRatingReadModel
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}

internal enum FlashcardReviewQueueCategory
{
    Learning,
    Relearning,
    Overdue,
    Due,
    New,
}

internal sealed record FlashcardReviewQueueCandidate(
    long CardId,
    FlashcardReviewQueueCategory Category,
    DateTimeOffset DueUtc);

internal sealed record FlashcardReviewLimitUsage(
    int ReviewCards,
    int NewCards);

internal sealed record FlashcardReviewQueueCounts(
    int LearningDueCards,
    int RelearningDueCards,
    int OverdueCards,
    int DueCards,
    int NewCards,
    int SuspendedCardsExcluded)
{
    internal int FutureReviewCards { get; init; }
}

internal sealed record FlashcardReviewSettingsReadModel(
    int MaximumReviewCardsPerDay,
    int MaximumNewCardsPerDay,
    double DesiredRetention,
    IReadOnlyList<TimeSpan>? LearningSteps = null,
    IReadOnlyList<TimeSpan>? RelearningSteps = null,
    bool IsOptimizationEnabled = false,
    IReadOnlyList<double>? FsrsParameters = null,
    bool IsCramEnabled = false,
    bool ShowFlashcardsInNavigationTree = false)
{
    private const int DefaultMaximumReviewCardsPerDay = 100;
    private const int DefaultMaximumNewCardsPerDay = 20;
    private const double DefaultDesiredRetention = 0.9d;
    private const double MinimumDesiredRetention = 0.7d;
    private const double MaximumDesiredRetention = 0.99d;
    private const bool DefaultIsOptimizationEnabled = false;
    private const bool DefaultIsCramEnabled = false;
    private const bool DefaultShowFlashcardsInNavigationTree = false;

    internal static IReadOnlyList<TimeSpan> DefaultLearningSteps { get; } =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(10),
    ];

    internal static IReadOnlyList<TimeSpan> DefaultRelearningSteps { get; } =
    [
        TimeSpan.FromMinutes(10),
    ];

    internal static FlashcardReviewSettingsReadModel Default { get; } = new(
        DefaultMaximumReviewCardsPerDay,
        DefaultMaximumNewCardsPerDay,
        DefaultDesiredRetention,
        DefaultLearningSteps,
        DefaultRelearningSteps,
        DefaultIsOptimizationEnabled,
        FsrsParameters: null,
        IsCramEnabled: DefaultIsCramEnabled,
        ShowFlashcardsInNavigationTree: DefaultShowFlashcardsInNavigationTree);

    internal FlashcardReviewSettingsReadModel Normalize()
    {
        return this with
        {
            MaximumReviewCardsPerDay = Math.Max(0, MaximumReviewCardsPerDay),
            MaximumNewCardsPerDay = Math.Max(0, MaximumNewCardsPerDay),
            DesiredRetention = Math.Clamp(
                DesiredRetention,
                MinimumDesiredRetention,
                MaximumDesiredRetention),
            LearningSteps = NormalizeSteps(LearningSteps, DefaultLearningSteps),
            RelearningSteps = NormalizeSteps(RelearningSteps, DefaultRelearningSteps),
            FsrsParameters = NormalizeFsrsParameters(FsrsParameters),
        };
    }

    private static IReadOnlyList<TimeSpan> NormalizeSteps(
        IReadOnlyList<TimeSpan>? steps,
        IReadOnlyList<TimeSpan> fallback)
    {
        TimeSpan[] normalized = (steps ?? [])
            .Where(step => step > TimeSpan.Zero)
            .ToArray();

        return normalized.Length > 0
            ? normalized
            : fallback.ToArray();
    }

    private static IReadOnlyList<double>? NormalizeFsrsParameters(IReadOnlyList<double>? parameters)
    {
        double[] normalized = (parameters ?? [])
            .Where(double.IsFinite)
            .ToArray();

        return normalized.Length == 21
            ? normalized
            : null;
    }
}
