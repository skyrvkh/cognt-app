namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed record CardsFilterOptionsState(
    IReadOnlyList<CardFilterDeckRow> DeckRows,
    IReadOnlyList<CardFilterOptionRow> StatusRows,
    IReadOnlyList<CardFilterOptionRow> SourceRows,
    IReadOnlyList<CardFilterOptionRow> TagRows,
    IReadOnlyList<string> DeckNames,
    int TotalCardCount,
    CardsSummaryCounts SummaryCounts);

internal sealed record CardsSummaryCounts(
    int LearningCards,
    int RelearningCards,
    int DueCards);
