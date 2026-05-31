using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    IReadOnlyList<GeneratedCardContextCardReadModel> IFlashcardNoteGenerationReader.LoadGeneratedCardsForSourceNote(
        string collectionId,
        string sourceRelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionPath(collectionPath, logger);
        return repository.LoadActiveGeneratedCardsForSourceNote(sourceRelPath)
            .Where(record =>
                !string.IsNullOrWhiteSpace(record.SourceContextId)
                && !string.IsNullOrWhiteSpace(record.SourceSlotId)
                && !string.IsNullOrWhiteSpace(record.SourceSnapshotHash))
            .Select(record => new GeneratedCardContextCardReadModel(
                record.Id,
                record.SourceRelPath,
                record.SourceContextId!,
                record.SourceSlotId!,
                record.SourceSnapshotHash!))
            .ToArray();
    }
}
