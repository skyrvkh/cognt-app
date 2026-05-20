using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public enum FlashcardReviewQueueCategory
{
    Learning = 0,
    Relearning = 1,
    Overdue = 2,
    Due = 3,
    New = 4,
}

public sealed record FlashcardReviewQueueCandidate(
    long CardId,
    FlashcardReviewQueueCategory Category,
    DateTimeOffset DueUtc);

public sealed record FlashcardReviewLimitUsage(
    int ReviewCardsReviewedToday,
    int NewCardsReviewedToday);

public sealed record FlashcardReviewQueueCounts(
    int LearningDueCards,
    int RelearningDueCards,
    int OverdueCards,
    int DueCards,
    int NewCards,
    int SuspendedCardsExcluded)
{
    public int ReviewDueCards => OverdueCards + DueCards;

    public int FutureReviewCards { get; init; }
}

public sealed record FlashcardReviewQueuePlan(
    IReadOnlyList<long> CardIds,
    FlashcardReviewQueueSummaryReadModel Summary);

public static partial class FlashcardReviewQueuePlanner
{
    public static FlashcardReviewQueuePlan CreatePlan(
        IReadOnlyList<FlashcardReviewQueueCandidate> candidates,
        FlashcardReviewQueueCounts counts,
        FlashcardReviewSettingsReadModel settings,
        FlashcardReviewLimitUsage usage)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(usage);

        int remainingReviewCards = RemainingDailyLimit(
            settings.MaximumReviewCardsPerDay,
            usage.ReviewCardsReviewedToday);
        int remainingNewCards = RemainingDailyLimit(
            settings.MaximumNewCardsPerDay,
            usage.NewCardsReviewedToday);

        IReadOnlyList<FlashcardReviewQueueCandidate> inProgressCandidates = SelectCandidates(
            candidates,
            IsInProgressCategory);
        IReadOnlyList<FlashcardReviewQueueCandidate> limitedReviewCandidates = SelectCandidates(
            candidates,
            IsReviewCategory,
            remainingReviewCards);
        IReadOnlyList<FlashcardReviewQueueCandidate> limitedNewCandidates = SelectCandidates(
            candidates,
            IsNewCategory,
            remainingNewCards);

        IReadOnlyList<long> cardIds = inProgressCandidates
            .Concat(limitedReviewCandidates)
            .Concat(limitedNewCandidates)
            .Select(candidate => candidate.CardId)
            .ToArray();

        FlashcardReviewQueueSummaryReadModel summary = CreateSummary(
            counts,
            limitedReviewCandidates.Count,
            limitedNewCandidates.Count,
            cardIds.Count);

        return new FlashcardReviewQueuePlan(cardIds, summary);
    }
}
