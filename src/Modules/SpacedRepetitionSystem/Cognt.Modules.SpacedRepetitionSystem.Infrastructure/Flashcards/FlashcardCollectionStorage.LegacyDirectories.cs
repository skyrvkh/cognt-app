namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private static void MigrateLegacyFlashcardsDirectory(string root)
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
        catch
        {
            if (movedToTemporaryDirectory
                && Directory.Exists(temporaryDirectory)
                && !Directory.Exists(legacyDirectory))
            {
                try
                {
                    Directory.Move(temporaryDirectory, legacyDirectory);
                }
                catch
                {
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
