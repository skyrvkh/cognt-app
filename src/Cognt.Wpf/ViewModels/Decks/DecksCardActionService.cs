using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using CogntApp.ViewModels.Decks.PresentationModels;


namespace CogntApp.ViewModels.Decks;

internal sealed class DecksCardActionService(
    IUpdateFlashcardSchedulingCommand updateFlashcardSchedulingCommand,
    IUpdateFlashcardContentCommand updateFlashcardContentCommand,
    IDeleteFlashcardsCommand deleteFlashcardsCommand,
    IUpdateFlashcardTagsCommand updateFlashcardTagsCommand,
    IDeleteFlashcardDeckCommand deleteFlashcardDeckCommand) : IDecksCardActionService
{
    private readonly IUpdateFlashcardSchedulingCommand updateFlashcardSchedulingCommand =
        updateFlashcardSchedulingCommand ?? throw new ArgumentNullException(nameof(updateFlashcardSchedulingCommand));
    private readonly IUpdateFlashcardContentCommand updateFlashcardContentCommand =
        updateFlashcardContentCommand ?? throw new ArgumentNullException(nameof(updateFlashcardContentCommand));
    private readonly IDeleteFlashcardsCommand deleteFlashcardsCommand =
        deleteFlashcardsCommand ?? throw new ArgumentNullException(nameof(deleteFlashcardsCommand));
    private readonly IUpdateFlashcardTagsCommand updateFlashcardTagsCommand =
        updateFlashcardTagsCommand ?? throw new ArgumentNullException(nameof(updateFlashcardTagsCommand));
    private readonly IDeleteFlashcardDeckCommand deleteFlashcardDeckCommand =
        deleteFlashcardDeckCommand ?? throw new ArgumentNullException(nameof(deleteFlashcardDeckCommand));


    public async Task<string> SetCardsSuspensionAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        bool isSuspended)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(cards);

        CardLibraryRow[] distinctCards = CardLibraryRowSelection.DistinctSchedulable(cards);
        if (distinctCards.Length == 0)
        {
            return "No saved cards selected.";
        }


        foreach (CardLibraryRow card in distinctCards)
        {
            await updateFlashcardSchedulingCommand.SetSuspensionAsync(
                new UpdateFlashcardSuspensionRequest(collectionId, card.Id, isSuspended));
        }

        string cardLabel = distinctCards.Length == 1 ? "Card" : $"{distinctCards.Length} cards";
        string actionLabel = isSuspended ? "suspended" : "unsuspended";
        return $"{cardLabel} {actionLabel}.";
    }

    public async Task<string> PostponeCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        int days)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(cards);

        CardLibraryRow[] distinctCards = CardLibraryRowSelection.DistinctSchedulable(cards);
        if (distinctCards.Length == 0)
        {
            return "No saved cards selected.";
        }


        foreach (CardLibraryRow card in distinctCards)
        {
            await updateFlashcardSchedulingCommand.PostponeDueDateAsync(
                new PostponeFlashcardDueDateRequest(collectionId, card.Id, days));
        }

        string cardLabel = distinctCards.Length == 1 ? "Card" : $"{distinctCards.Length} cards";
        return $"{cardLabel} postponed by {days} day{(days == 1 ? string.Empty : "s")}.";
    }

    public async Task<string> UpdateCardContentAsync(
        string collectionId,
        CardLibraryRow card,
        CardEditResult edit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(edit);

        if (card.Id <= 0)
        {
            throw new InvalidOperationException("Select a saved card before editing.");
        }


        await updateFlashcardContentCommand.ExecuteAsync(
            new UpdateFlashcardContentRequest(
                collectionId,
                card.Id,
                edit.FrontContent,
                edit.BackContent,
                edit.DeckName,
                edit.Tags));

        return "Card updated.";
    }

    public async Task<string> DeleteCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(cards);

        long[] cardIds = CardLibraryRowSelection.DistinctPersistedIds(cards);

        int deletedCards = await deleteFlashcardsCommand.ExecuteAsync(
            new DeleteFlashcardsRequest(collectionId, cardIds));

        return deletedCards == 1
            ? "Card deleted."
            : $"{deletedCards} cards deleted.";
    }

    public async Task<string> DeleteDecksAsync(
        string collectionId,
        IReadOnlyList<DeckRow> decks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(decks);

        long[] deckIds = decks
            .Where(deck => deck.Id > 0)
            .Select(deck => deck.Id)
            .Distinct()
            .ToArray();

        int deletedDecks = await deleteFlashcardDeckCommand.ExecuteAsync(
            new DeleteFlashcardDeckRequest(collectionId, deckIds));

        return deletedDecks switch
        {
            0 => "Default deck is required as the fallback deck and was not deleted.",
            1 => "Deck deleted. Cards moved to Default.",
            _ => $"{deletedDecks} decks deleted. Cards moved to Default."
        };
    }

    public async Task<string> AddTagsToCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        long[] cardIds = CardLibraryRowSelection.DistinctPersistedIds(cards);

        int updatedCards = await updateFlashcardTagsCommand.AddTagsAsync(
            new AddFlashcardTagsRequest(collectionId, cardIds, tags));

        string tagLabel = string.Join(", ", tags);
        return updatedCards == 0
            ? $"Tags already assigned or unsupported for selected cards: {tagLabel}."
            : $"Added {tagLabel} to {updatedCards} card{(updatedCards == 1 ? string.Empty : "s")}.";
    }

    public async Task<string> RemoveTagsFromCardsAsync(
        string collectionId,
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        long[] cardIds = CardLibraryRowSelection.DistinctPersistedIds(cards);

        int updatedCards = await updateFlashcardTagsCommand.RemoveTagsAsync(
            new RemoveFlashcardTagsRequest(collectionId, cardIds, tags));

        string tagLabel = string.Join(", ", tags);
        return updatedCards == 0
            ? $"Tags were not assigned or unsupported for selected cards: {tagLabel}."
            : $"Removed {tagLabel} from {updatedCards} card{(updatedCards == 1 ? string.Empty : "s")}.";
    }
}
