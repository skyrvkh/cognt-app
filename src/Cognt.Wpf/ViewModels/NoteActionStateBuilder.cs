using System.IO;

namespace CogntApp.ViewModels;

internal static class NoteActionStateBuilder
{
    internal static string[] CreateDeletedFullNotePaths(
        string collectionPath,
        IReadOnlyList<string> deletedNotePaths)
    {
        HashSet<string> seenFullPaths = new(StringComparer.OrdinalIgnoreCase);
        List<string> fullPaths = new(deletedNotePaths.Count);
        foreach (string notePath in deletedNotePaths)
        {
            string fullPath = Path.GetFullPath(ResolveCollectionFilePath(collectionPath, notePath));
            if (seenFullPaths.Add(fullPath))
            {
                fullPaths.Add(fullPath);
            }
        }

        return fullPaths.ToArray();
    }

    internal static bool ContainsDeletedRelativePath(
        IReadOnlyList<string> deletedNotePaths,
        string currentNoteRelativePath)
    {
        foreach (string notePath in deletedNotePaths)
        {
            if (string.Equals(
                NormalizeRelativePath(notePath),
                currentNoteRelativePath,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string CreateDuplicateNoteTitle(string title)
    {
        string trimmedTitle = string.IsNullOrWhiteSpace(title)
            ? "Untitled"
            : title.Trim();
        return $"{trimmedTitle} Copy";
    }

    private static string ResolveCollectionFilePath(string collectionPath, string relativePath)
    {
        return Path.Combine(
            collectionPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }
}
