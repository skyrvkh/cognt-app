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
    double DesiredRetention)
{
    private const int DefaultMaximumReviewCardsPerDay = 100;
    private const int DefaultMaximumNewCardsPerDay = 20;
    private const double DefaultDesiredRetention = 0.9d;
    private const double MinimumDesiredRetention = 0.7d;
    private const double MaximumDesiredRetention = 0.99d;

    internal static FlashcardReviewSettingsReadModel Default { get; } = new(
        DefaultMaximumReviewCardsPerDay,
        DefaultMaximumNewCardsPerDay,
        DefaultDesiredRetention);

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
        };
    }
}
