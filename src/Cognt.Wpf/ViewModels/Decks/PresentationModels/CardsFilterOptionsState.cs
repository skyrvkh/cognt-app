namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed record CardsFilterOptionsState(
    IReadOnlyList<CardFilterDeckRow> DeckRows,
    IReadOnlyList<CardFilterOptionRow> StatusRows,
    IReadOnlyList<CardFilterOptionRow> SourceRows,
    IReadOnlyList<CardFilterOptionRow> TagRows);
