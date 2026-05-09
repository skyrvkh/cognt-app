using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

namespace CogntApp.ViewModels.Decks;

internal interface IDecksWorkspaceDataProvider
{
    DecksWorkspaceData Load(string? collectionId, FlashcardCardsQueryRequest filteredRequest);
}
