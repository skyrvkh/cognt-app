using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private const string DefaultDeckName = "Default";

    public async Task<int> CreateCardsFromContentAsync(
        string collectionId,
        string deckName,
        string content,
        IReadOnlyList<string> tags,
        string? sourceRelPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<int> clozeIndexes = FlashcardCardContentCodec.GetClozeIndexes(content);

        if (clozeIndexes.Count == 0)
        {
            string plainText = BlockSuiteDocumentText.ToPlainText(content);
            await CreateBasicCardAsync(
                collectionId,
                deckName,
                content,
                BlockSuiteDocumentText.FromPlainText(plainText, "Back"),
                tags,
                sourceRelPath,
                cancellationToken);
            return 1;
        }

        int createdCards = await CreateClozeCardsAsync(
            collectionId,
            deckName,
            content,
            tags,
            sourceRelPath,
            clozeIndexes,
            cancellationToken);
        return createdCards;
    }

    public Task UpdateCardFrontContentAsync(
        string collectionId,
        long cardId,
        string frontContent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        FlashcardRecord card = repository.GetCard(cardId);
        string existingContent = FlashcardCollectionStorage.ReadFlashcardFile(collectionPath, card.FrontRelPath);
        FlashcardCollectionStorage.WriteFlashcardFile(
            collectionPath,
            card.FrontRelPath,
            FlashcardCardContentCodec.ReplaceEditableContent(existingContent, frontContent));
        return Task.CompletedTask;
    }

    public Task UpdateCardContentAsync(
        string collectionId,
        long cardId,
        string frontContent,
        string? backContent,
        string deckName,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckName);
        ArgumentNullException.ThrowIfNull(frontContent);
        ArgumentNullException.ThrowIfNull(tags);
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        FlashcardRecord card = repository.GetCard(cardId);
        string existingContent = FlashcardCollectionStorage.ReadFlashcardFile(collectionPath, card.FrontRelPath);
        if (!FlashcardCardContentCodec.TryUpdateEditableContent(
                existingContent,
                frontContent,
                backContent,
                tags,
                out string updatedContent,
                out string error))
        {
            throw new InvalidOperationException(error);
        }

        long deckId = repository.EnsureDeck(deckName);
        repository.UpdateCardDeck(cardId, deckId);
        FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, card.FrontRelPath, updatedContent);
        return Task.CompletedTask;
    }

    public Task<int> DeleteCardsAsync(
        string collectionId,
        IReadOnlyList<long> cardIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardIds);
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        IReadOnlyList<FlashcardRecord> activeCards = repository.LoadCardsByIds(cardIds);
        repository.MarkCardsDeleted(CreateCardIds(activeCards));
        return Task.FromResult(activeCards.Count);
    }

    public Task<int> DeleteDecksAsync(
        string collectionId,
        IReadOnlyList<long> deckIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deckIds);
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        int deletedDecks = repository.DeleteDecksReassignCards(deckIds, DefaultDeckName);
        return Task.FromResult(deletedDecks);
    }

    public Task<int> AddTagsToCardsAsync(
        string collectionId,
        IReadOnlyList<long> cardIds,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardIds);
        ArgumentNullException.ThrowIfNull(tags);
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        int updatedCards = 0;
        foreach (FlashcardRecord card in repository.LoadCardsByIds(cardIds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string existingContent = FlashcardCollectionStorage.ReadFlashcardFile(collectionPath, card.FrontRelPath);
            if (!FlashcardCardContentCodec.TryAddTags(existingContent, tags, out string updatedContent))
            {
                continue;
            }

            FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, card.FrontRelPath, updatedContent);
            updatedCards++;
        }
        return Task.FromResult(updatedCards);
    }

    public Task<int> RemoveTagsFromCardsAsync(
        string collectionId,
        IReadOnlyList<long> cardIds,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardIds);
        ArgumentNullException.ThrowIfNull(tags);
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath, logger);
        int updatedCards = 0;
        foreach (FlashcardRecord card in repository.LoadCardsByIds(cardIds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string existingContent = FlashcardCollectionStorage.ReadFlashcardFile(collectionPath, card.FrontRelPath);
            if (!FlashcardCardContentCodec.TryRemoveTags(existingContent, tags, out string updatedContent))
            {
                continue;
            }

            FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, card.FrontRelPath, updatedContent);
            updatedCards++;
        }
        return Task.FromResult(updatedCards);
    }
}
