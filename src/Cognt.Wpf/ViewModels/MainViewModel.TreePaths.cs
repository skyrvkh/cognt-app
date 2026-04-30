using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private string? ResolveNotePath(KnowledgeTreeItemReadModel item)
    {
        return item.Kind == KnowledgeItemKind.Note && !string.IsNullOrWhiteSpace(item.FileRelPath)
            ? ResolveCollectionFilePath(openCollectionPath, item.FileRelPath)
            : null;
    }

    private static string ResolveCollectionFilePath(string collectionPath, string relativePath)
    {
        return Path.Combine(
            collectionPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string GetRequiredCurrentNoteRelativePath()
    {
        return GetRequiredRelativeNotePath(currentNotePath);
    }

    private string? TryGetCurrentNoteRelativePath()
    {
        return TryGetRelativeNotePath(currentNotePath);
    }

    private string GetRequiredRelativeNotePath(string? notePath)
    {
        return TryGetRelativeNotePath(notePath)
            ?? throw new InvalidOperationException("The note must be inside the current collection.");
    }

    private string? TryGetRelativeNotePath(string? notePath)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || string.IsNullOrWhiteSpace(notePath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(notePath);
        string root = Path.GetFullPath(openCollectionPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = string.Concat(root, Path.DirectorySeparatorChar);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }
}
