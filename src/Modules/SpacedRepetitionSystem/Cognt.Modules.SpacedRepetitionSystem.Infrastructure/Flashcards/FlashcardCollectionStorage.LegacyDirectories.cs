using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private static void MigrateLegacyFlashcardsDirectory(
        string root,
        Microsoft.Extensions.Logging.ILogger? logger)
    {
        if (!IsExactChildDirectory(root, LegacyFlashcardsDirectoryName)
            || IsExactChildDirectory(root, FlashcardsDirectoryName))
        {
            return;
        }

        string legacyDirectory = Path.Combine(root, LegacyFlashcardsDirectoryName);
        string currentDirectory = Path.Combine(root, FlashcardsDirectoryName);
        string temporaryDirectory = Path.Combine(root, $".cognt-flashcards-rename-{Guid.NewGuid():N}");
        bool movedToTemporaryDirectory = false;
        try
        {
            Directory.Move(legacyDirectory, temporaryDirectory);
            movedToTemporaryDirectory = true;
            Directory.Move(temporaryDirectory, currentDirectory);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Legacy flashcards directory migration failed. Root={Root} LegacyDirectory={LegacyDirectory} CurrentDirectory={CurrentDirectory} TemporaryDirectory={TemporaryDirectory}",
                root,
                legacyDirectory,
                currentDirectory,
                temporaryDirectory);

            if (movedToTemporaryDirectory
                && Directory.Exists(temporaryDirectory)
                && !Directory.Exists(legacyDirectory))
            {
                try
                {
                    Directory.Move(temporaryDirectory, legacyDirectory);
                }
                catch (Exception rollbackException)
                {
                    logger?.LogWarning(
                        rollbackException,
                        "Failed to roll back flashcards directory migration. Root={Root} TemporaryDirectory={TemporaryDirectory} LegacyDirectory={LegacyDirectory}",
                        root,
                        temporaryDirectory,
                        legacyDirectory);
                }
            }

            throw;
        }
    }

    private static bool IsExactChildDirectory(string root, string directoryName)
    {
        return Directory.Exists(root)
            && Directory.EnumerateDirectories(root)
                .Select(path => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)))
                .Any(name => string.Equals(name, directoryName, StringComparison.Ordinal));
    }
}
