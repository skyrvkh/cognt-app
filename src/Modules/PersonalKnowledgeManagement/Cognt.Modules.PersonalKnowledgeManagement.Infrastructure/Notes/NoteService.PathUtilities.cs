using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static bool IsSafeCurrentNotePath(string path, string currentPath, string root)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(currentPath)
            || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string fullCurrentPath = Path.GetFullPath(currentPath);
        string fullRoot = NormalizeRoot(root);

        return string.Equals(fullPath, fullCurrentPath, StringComparison.OrdinalIgnoreCase)
            && IsPathInsideRoot(fullPath, fullRoot)
            && IsNoteFilePath(fullPath);
    }

    private static string ResolveExistingCollectionRoot(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        string root = NormalizeRoot(collectionId);
        return Directory.Exists(root)
            ? root
            : throw new DirectoryNotFoundException($"The collection directory does not exist: {root}");
    }

    private static string NormalizeRoot(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsPathInsideRoot(string fullPath, string fullRoot)
    {
        string rootWithSeparator = $"{fullRoot}{Path.DirectorySeparatorChar}";

        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static string ToPlatformPath(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static bool TryCreateNoteFilesDirectoryAlias(
        string normalizedRelativePath,
        out string? aliasRelativePath)
    {
        aliasRelativePath = TrySwapFirstPathSegment(
            normalizedRelativePath,
            LegacyFilesDirectoryName,
            FilesDirectoryName)
            ?? TrySwapFirstPathSegment(
                normalizedRelativePath,
                FilesDirectoryName,
                LegacyFilesDirectoryName);

        return aliasRelativePath is not null;
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
