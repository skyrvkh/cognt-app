using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public sealed record FlashcardReviewCardReadModel(
    long Id,
    string FrontContent,
    string BackContent,
    string FrontText,
    string BackText,
    FlashcardContentKind FrontContentKind = FlashcardContentKind.LegacyText,
    FlashcardContentKind BackContentKind = FlashcardContentKind.LegacyText,
    FlashcardMaterializationStatus MaterializationStatus = FlashcardMaterializationStatus.Ok,
    MultimodalClozeReadModel? FrontMultimodalContent = null,
    MultimodalClozeReadModel? BackMultimodalContent = null)
{
    public bool WasDueAtQueueStart { get; init; }
}

public enum FlashcardReviewQueueMode
{
    Scheduled = 0,
    Cram = 1,
}

public sealed record FlashcardReviewSettingsReadModel(
    int MaximumReviewCardsPerDay,
    int MaximumNewCardsPerDay,
    double DesiredRetention,
    IReadOnlyList<TimeSpan>? LearningSteps = null,
    IReadOnlyList<TimeSpan>? RelearningSteps = null,
    bool IsOptimizationEnabled = false,
    IReadOnlyList<double>? FsrsParameters = null)
{
    public const int DefaultMaximumReviewCardsPerDay = 100;
    public const int DefaultMaximumNewCardsPerDay = 20;
    public const double DefaultDesiredRetention = 0.9d;
    public const double MinimumDesiredRetention = 0.7d;
    public const double MaximumDesiredRetention = 0.99d;
    public const bool DefaultIsOptimizationEnabled = false;
    public const int FsrsParameterCount = 21;

    public static IReadOnlyList<TimeSpan> DefaultLearningSteps { get; } =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(10),
    ];

    public static IReadOnlyList<TimeSpan> DefaultRelearningSteps { get; } =
    [
        TimeSpan.FromMinutes(10),
    ];

    public static IReadOnlyList<double> MinimumFsrsParameters { get; } =
    [
        0.001,
        0.001,
        0.001,
        0.001,
        1.0,
        0.001,
        0.001,
        0.001,
        0.0,
        0.0,
        0.001,
        0.001,
        0.001,
        0.001,
        0.0,
        0.0,
        1.0,
        0.0,
        0.0,
        0.0,
        0.1,
    ];

    public static IReadOnlyList<double> MaximumFsrsParameters { get; } =
    [
        100.0,
        100.0,
        100.0,
        100.0,
        10.0,
        4.0,
        4.0,
        0.75,
        4.5,
        0.8,
        3.5,
        5.0,
        0.25,
        0.9,
        4.0,
        1.0,
        6.0,
        2.0,
        2.0,
        0.8,
        0.8,
    ];

    public static FlashcardReviewSettingsReadModel Default { get; } = new(
        DefaultMaximumReviewCardsPerDay,
        DefaultMaximumNewCardsPerDay,
        DefaultDesiredRetention,
        DefaultLearningSteps,
        DefaultRelearningSteps,
        DefaultIsOptimizationEnabled,
        FsrsParameters: null);

    public FlashcardReviewSettingsReadModel Normalize()
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

    public static bool IsValidFsrsParameter(int index, double value)
    {
        return index >= 0
            && index < FsrsParameterCount
            && double.IsFinite(value)
            && value >= MinimumFsrsParameters[index]
            && value <= MaximumFsrsParameters[index];
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
        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Length != FsrsParameterCount)
        {
            return null;
        }

        return normalized
            .Where((parameter, index) => IsValidFsrsParameter(index, parameter))
            .Count() == FsrsParameterCount
            ? normalized
            : null;
    }
}

public sealed record FlashcardReviewQueueReadModel(
    IReadOnlyList<FlashcardReviewCardReadModel> Cards,
    FlashcardReviewQueueSummaryReadModel Summary,
    DateTimeOffset? NextDueUtc = null,
    DateTimeOffset? NextIntradayDueUtc = null)
{
    public FlashcardReviewQueueMode Mode { get; init; } = FlashcardReviewQueueMode.Scheduled;
}

public sealed record FlashcardReviewQueueSummaryReadModel(
    int LearningDueCards,
    int RelearningDueCards,
    int OverdueCards,
    int DueCards,
    int NewCards,
    int SuspendedCardsExcluded,
    int ReviewLimitExcluded,
    int NewLimitExcluded,
    int TotalCards)
{
    public int FutureReviewCards { get; init; }

    public int SelectedCards { get; init; }

    public int SkippedInvalidCards { get; init; }
}
