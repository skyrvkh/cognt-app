namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private static string GetDatabasePath(string collectionPath)
    {
        return Path.Combine(collectionPath, MetadataDatabaseFileName);
    }

    private static bool FlashcardFileExists(string root, string relPath)
    {
        if (!TryGetFlashcardFullPath(root, relPath, out string fullPath))
        {
            return false;
        }

        return File.Exists(fullPath)
            || TryGetFlashcardDirectoryAliasFullPath(root, relPath, out string aliasFullPath)
            && File.Exists(aliasFullPath);
    }

    private static bool TryGetFlashcardFullPath(
        string root,
        string relPath,
        out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar)));
            return IsFullPathInsideRoot(fullPath, root);
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool IsFullPathInsideRoot(string fullPath, string root)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFlashcardFullPath(string root, string relPath)
    {
        if (!TryGetFlashcardFullPath(root, relPath, out string fullPath))
        {
            throw new InvalidOperationException($"The flashcard path is outside the collection: {relPath}");
        }

        return fullPath;
    }

    private static string GetExistingFlashcardFullPath(string root, string relPath)
    {
        string fullPath = GetFlashcardFullPath(root, relPath);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        return TryGetFlashcardDirectoryAliasFullPath(root, relPath, out string aliasFullPath)
            && File.Exists(aliasFullPath)
            ? aliasFullPath
            : fullPath;
    }

    private static bool TryGetFlashcardDirectoryAliasFullPath(
        string root,
        string relPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        string normalizedRelPath = relPath.Replace('\\', '/');
        string? aliasRelPath = TrySwapFirstPathSegment(
            normalizedRelPath,
            LegacyFlashcardsDirectoryName,
            FlashcardsDirectoryName)
            ?? TrySwapFirstPathSegment(
                normalizedRelPath,
                FlashcardsDirectoryName,
                LegacyFlashcardsDirectoryName);

        return aliasRelPath is not null
            && TryGetFlashcardFullPath(root, aliasRelPath, out fullPath);
    }

    private static string? TrySwapFirstPathSegment(
        string normalizedRelativePath,
        string from,
        string to)
    {
        if (normalizedRelativePath.Length == from.Length
            && string.Equals(normalizedRelativePath, from, StringComparison.OrdinalIgnoreCase))
        {
            return to;
        }

        if (normalizedRelativePath.Length > from.Length
            && normalizedRelativePath[from.Length] == '/'
            && normalizedRelativePath.StartsWith(from, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(to, normalizedRelativePath.AsSpan(from.Length));
        }

        return null;
    }
}
