using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private static void PruneMissingActiveCardFiles(
        string root,
        SqliteFlashcardRepository repository,
        Microsoft.Extensions.Logging.ILogger? logger)
    {
        long[] missingCardIds = repository
            .LoadActiveCardFileReferences()
            .Where(card => !FlashcardFileExists(root, card.FrontRelPath))
            .Select(card => card.Id)
            .ToArray();

        if (missingCardIds.Length == 0)
        {
            return;
        }

        repository.MarkCardsDeleted(missingCardIds);
        logger?.LogWarning(
            "Pruned active flashcards with missing content files. Root={Root} MissingCardCount={MissingCardCount}",
            root,
            missingCardIds.Length);
    }

    private static MaterializedFlashcardContent LoadMaterializedContent(
        string root,
        string frontRelPath)
    {
        string frontFileContent = ReadFlashcardFile(root, frontRelPath);
        return FlashcardCardContentCodec.MaterializeSingleFile(frontFileContent, root);
    }
}
