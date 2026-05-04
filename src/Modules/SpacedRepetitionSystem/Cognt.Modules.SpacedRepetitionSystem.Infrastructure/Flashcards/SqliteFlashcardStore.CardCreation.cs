using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private Task<long> CreateBasicCardAsync(
        string collectionId,
        string deckName,
        string frontContent,
        string backContent,
        IReadOnlyList<string> tags,
        string? sourceRelPath,
        CancellationToken cancellationToken = default)
    {
        string document = FlashcardCardContentCodec.CreateBasicDocument(frontContent, backContent, tags);
        return CreateSingleFileCardAsync(
            collectionId,
            deckName,
            FlashcardCardContentCodec.BasicCardType,
            document,
            sourceRelPath,
            null,
            cancellationToken);
    }

    private async Task<int> CreateClozeCardsAsync(
        string collectionId,
        string deckName,
        string content,
        IReadOnlyList<string> tags,
        string? sourceRelPath,
        IReadOnlyList<int> clozeIndexes,
        CancellationToken cancellationToken)
    {
        int created = 0;
        foreach (int clozeIndex in clozeIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await CreateSingleFileCardAsync(
                collectionId,
                deckName,
                FlashcardCardContentCodec.ClozeCardType,
                FlashcardCardContentCodec.CreateClozeDocument(content, clozeIndex, tags),
                sourceRelPath,
                clozeIndex,
                cancellationToken);
            created++;
        }

        return created;
    }

    private async Task<long> CreateSingleFileCardAsync(
        string collectionId,
        string deckName,
        string cardType,
        string content,
        string? sourceRelPath,
        int? clozeIndex,
        CancellationToken cancellationToken)
    {
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        string relPath = FlashcardCollectionStorage.CreateFlashcardRelPath();
        FlashcardSchedule initialSchedule = await scheduler.CreateNewCardAsync(cancellationToken);

        FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, relPath, content);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        long deckId = repository.EnsureDeck(deckName);
        return repository.CreateCard(
            deckId,
            cardType,
            relPath,
            sourceRelPath,
            sourceContextId: null,
            sourceSlotId: null,
            sourceTargetId: null,
            sourceSnapshotHash: null,
            clozeIndex,
            initialSchedule);
    }

}
