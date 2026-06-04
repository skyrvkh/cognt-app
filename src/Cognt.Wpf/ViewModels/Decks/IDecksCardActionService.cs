using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

internal interface IDecksCardActionService
{
    Task<string> SetCardsSuspensionAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        bool isSuspended);

    Task<string> PostponeCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        int days);

    Task<string> UpdateCardContentAsync(
        string collectionId,
        CardLibraryRow card,
        CardEditResult edit);

    Task<string> DeleteCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards);

    Task<string> DeleteDecksAsync(
        string collectionId,
        IReadOnlyList<DeckRow> decks);

    Task<string> AddTagsToCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags);

    Task<string> RemoveTagsFromCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags);
}
