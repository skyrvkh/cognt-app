using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    public async Task<CreateCardsFromCardContextResult> CreateCardsFromCardContextAsync(
        string collectionId,
        string deckName,
        string sourceRelPath,
        CardContextSnapshot context,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        IReadOnlyList<ExpandedCardContextCard> expandedCards = CardContextCardExpansion.Expand(context);
        string[] cardTags = CreateDistinctCardTags(tags);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        long deckId = repository.EnsureDeck(deckName);
        Dictionary<string, GeneratedFlashcardSourceRecord> existingBySlotId = LoadFirstGeneratedCardsBySlotId(
            repository,
            sourceRelPath,
            context.Id);

        List<long> affectedCardIds = [];
        HashSet<string> currentSlotIds = new(StringComparer.Ordinal);
        int createdCards = 0;
        int updatedCards = 0;

        foreach (ExpandedCardContextCard card in expandedCards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentSlotIds.Add(card.CurrentSlotId);

            string document = FlashcardCardContentCodec.CreateMultimodalClozeDocument(card, cardTags);
            string? sourceTargetId = card.CurrentTargetIds.Count > 0 ? card.CurrentTargetIds[0] : null;

            if (existingBySlotId.TryGetValue(card.CurrentSlotId, out GeneratedFlashcardSourceRecord? existing))
            {
                FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, existing.FrontRelPath, document);
                repository.UpdateGeneratedCard(
                    existing.Id,
                    deckId,
                    FlashcardCardContentCodec.MultimodalClozeCardType,
                    sourceRelPath,
                    context.Id,
                    card.CurrentSlotId,
                    sourceTargetId,
                    card.SnapshotHash);
                affectedCardIds.Add(existing.Id);
                updatedCards++;
                continue;
            }

            string relPath = FlashcardCollectionStorage.CreateFlashcardRelPath();
            FlashcardSchedule initialSchedule = await scheduler.CreateNewCardAsync(cancellationToken);
            FlashcardCollectionStorage.WriteFlashcardFile(collectionPath, relPath, document);
            long cardId = repository.CreateCard(
                deckId,
                FlashcardCardContentCodec.MultimodalClozeCardType,
                relPath,
                sourceRelPath,
                context.Id,
                card.CurrentSlotId,
                sourceTargetId,
                card.SnapshotHash,
                clozeIndex: null,
                initialSchedule);
            affectedCardIds.Add(cardId);
            createdCards++;
        }

        string[] orphanedSlotIds = CreateOrphanedSlotIds(existingBySlotId, currentSlotIds);

        return new CreateCardsFromCardContextResult(
            createdCards,
            updatedCards,
            affectedCardIds,
            orphanedSlotIds);
    }

    private static string[] CreateDistinctCardTags(IReadOnlyList<string> tags)
    {
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> distinctTags = new(tags.Count);
        foreach (string tag in tags)
        {
            if (seenTags.Add(tag))
            {
                distinctTags.Add(tag);
            }
        }

        return distinctTags.ToArray();
    }

    private static Dictionary<string, GeneratedFlashcardSourceRecord> LoadFirstGeneratedCardsBySlotId(
        SqliteFlashcardRepository repository,
        string sourceRelPath,
        string sourceContextId)
    {
        IReadOnlyList<GeneratedFlashcardSourceRecord> records = repository.LoadActiveGeneratedCardsForContext(
            sourceRelPath,
            sourceContextId);
        Dictionary<string, GeneratedFlashcardSourceRecord> existingBySlotId = new(StringComparer.Ordinal);
        foreach (GeneratedFlashcardSourceRecord record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.SourceSlotId))
            {
                existingBySlotId.TryAdd(record.SourceSlotId, record);
            }
        }

        return existingBySlotId;
    }

    private static string[] CreateOrphanedSlotIds(
        IReadOnlyDictionary<string, GeneratedFlashcardSourceRecord> existingBySlotId,
        IReadOnlySet<string> currentSlotIds)
    {
        List<string> orphanedSlotIds = [];
        foreach (KeyValuePair<string, GeneratedFlashcardSourceRecord> pair in existingBySlotId)
        {
            if (!currentSlotIds.Contains(pair.Key)
                && !string.IsNullOrWhiteSpace(pair.Value.SourceSlotId))
            {
                orphanedSlotIds.Add(pair.Value.SourceSlotId);
            }
        }

        return orphanedSlotIds.ToArray();
    }
}
