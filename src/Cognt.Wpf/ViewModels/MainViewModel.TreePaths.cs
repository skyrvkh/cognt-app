using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private string? ResolveNotePath(KnowledgeTreeItemReadModel item)
    {
        string? notePath = item.Kind == KnowledgeItemKind.Note && !string.IsNullOrWhiteSpace(item.FileRelPath)
            ? ResolveCollectionFilePath(openCollectionPath, item.FileRelPath)
            : null;
        return notePath;
    }

    private static string ResolveCollectionFilePath(string collectionPath, string relativePath)
    {
        return Path.Combine(
            collectionPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string GetRequiredCurrentNoteRelativePath()
    {
        string relativePath = GetRequiredRelativeNotePath(currentNotePath);
        return relativePath;
    }

    private string? TryGetCurrentNoteRelativePath()
    {
        string? relativePath = TryGetRelativeNotePath(currentNotePath);
        return relativePath;
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
            logger?.LogWarning("Note path is outside current collection. CollectionRoot={CollectionRoot} NotePath={NotePath}", root, fullPath);
            return null;
        }

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
        return relativePath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }
}
