namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public sealed record FlashcardProgressAnalyticsReadModel(
    int DueTomorrowCards,
    int DueTomorrowDecks,
    IReadOnlyList<FlashcardDeckReviewHistoryReadModel> DeckReviewHistory,
    IReadOnlyList<FlashcardReviewRatingCountReadModel> RecallDistribution,
    IReadOnlyList<FlashcardWeeklyReviewOutcomeReadModel> WeeklyReviewOutcomes);

public sealed record FlashcardDeckReviewHistoryReadModel(
    string DeckName,
    int ReviewedCount,
    int RetainedCount);

public sealed record FlashcardReviewRatingCountReadModel(
    FlashcardReviewRatingReadModel Rating,
    int Count);

public sealed record FlashcardWeeklyReviewOutcomeReadModel(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int Again,
    int Hard,
    int Good,
    int Easy);
