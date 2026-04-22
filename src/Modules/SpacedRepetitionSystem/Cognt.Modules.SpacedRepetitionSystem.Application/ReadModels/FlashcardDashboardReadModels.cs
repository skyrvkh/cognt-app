namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public sealed record FlashcardDashboardSummaryReadModel(
    int TotalCards,
    int DueCards,
    int NewCards,
    int ReviewedTodayCards,
    int DifficultCards,
    int ReviewHistoryCount,
    IReadOnlyList<FlashcardReviewActivityReadModel> ReviewActivity);

public sealed record FlashcardReviewActivityReadModel(
    DateOnly Date,
    int ReviewCount);
