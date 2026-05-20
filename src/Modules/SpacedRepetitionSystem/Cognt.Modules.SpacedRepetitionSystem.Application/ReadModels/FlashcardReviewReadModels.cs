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
    double DesiredRetention)
{
    public const int DefaultMaximumReviewCardsPerDay = 100;
    public const int DefaultMaximumNewCardsPerDay = 20;
    public const double DefaultDesiredRetention = 0.9d;
    public const double MinimumDesiredRetention = 0.7d;
    public const double MaximumDesiredRetention = 0.99d;

    public static FlashcardReviewSettingsReadModel Default { get; } = new(
        DefaultMaximumReviewCardsPerDay,
        DefaultMaximumNewCardsPerDay,
        DefaultDesiredRetention);

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
        };
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
