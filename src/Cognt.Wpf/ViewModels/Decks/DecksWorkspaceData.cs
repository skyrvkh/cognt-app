using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels.Decks;

internal sealed record DecksWorkspaceData(
    IReadOnlyList<FlashcardDeckSummaryReadModel> Decks,
    IReadOnlyList<FlashcardCardSummaryReadModel> AllCards,
    IReadOnlyList<FlashcardCardSummaryReadModel> VisibleCards,
    string PracticeDueCount,
    IReadOnlyDictionary<string, string> SourceTitlesByRelPath);
