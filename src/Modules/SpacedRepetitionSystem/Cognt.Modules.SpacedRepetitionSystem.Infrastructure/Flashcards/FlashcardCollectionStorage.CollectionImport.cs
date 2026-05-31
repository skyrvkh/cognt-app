using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    internal static void DeleteFlashcardFileIfExists(
        string root,
        string relPath,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        if (!TryGetFlashcardFullPath(root, relPath, out string fullPath))
        {
            logger?.LogWarning(
                "Skipped deleting flashcard content file because the resolved path is outside the collection. Root={Root} RelativePath={RelativePath}",
                root,
                relPath);
            return;
        }

        if (!File.Exists(fullPath))
        {
            return;
        }

        File.Delete(fullPath);
    }
}
