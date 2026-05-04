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
        IReadOnlyList<string> mergedTags = tags
            .Concat(["source:note"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        long deckId = repository.EnsureDeck(deckName);
        Dictionary<string, GeneratedFlashcardSourceRecord> existingBySlotId = repository
            .LoadActiveGeneratedCardsForContext(sourceRelPath, context.Id)
            .Where(record => !string.IsNullOrWhiteSpace(record.SourceSlotId))
            .GroupBy(record => record.SourceSlotId!, StringComparer.Ordinal)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First(), StringComparer.Ordinal);

        List<long> affectedCardIds = [];
        HashSet<string> currentSlotIds = new(StringComparer.Ordinal);
        int createdCards = 0;
        int updatedCards = 0;

        foreach (ExpandedCardContextCard card in expandedCards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentSlotIds.Add(card.CurrentSlotId);

            string document = FlashcardCardContentCodec.CreateMultimodalClozeDocument(card, mergedTags);
            string? sourceTargetId = card.CurrentTargetIds.FirstOrDefault();

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

        IReadOnlyList<GeneratedFlashcardSourceRecord> orphanedCards = existingBySlotId
            .Where(pair => !currentSlotIds.Contains(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();

        return new CreateCardsFromCardContextResult(
            createdCards,
            updatedCards,
            affectedCardIds,
            orphanedCards
                .Select(card => card.SourceSlotId)
                .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
                .Select(slotId => slotId!)
                .ToArray());
    }
}
