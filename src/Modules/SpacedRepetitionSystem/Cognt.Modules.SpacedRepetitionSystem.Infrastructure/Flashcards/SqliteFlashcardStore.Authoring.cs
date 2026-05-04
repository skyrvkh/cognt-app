using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
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

        return await CreateClozeCardsAsync(
            collectionId,
            deckName,
            content,
            tags,
            sourceRelPath,
            clozeIndexes,
            cancellationToken);
    }

    public Task UpdateCardFrontContentAsync(
        string collectionId,
        long cardId,
        string frontContent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        FlashcardRecord card = repository.GetCard(cardId);
        string existingContent = FlashcardCollectionStorage.ReadFlashcardFile(collectionPath, card.FrontRelPath);
        FlashcardCollectionStorage.WriteFlashcardFile(
            collectionPath,
            card.FrontRelPath,
            FlashcardCardContentCodec.ReplaceEditableContent(existingContent, frontContent));
        return Task.CompletedTask;
    }

}
