using System.IO;

namespace CogntApp.ViewModels;

internal sealed record NoteSaveRequest(
    string FullNotePath,
    string CollectionPath,
    string RelativePath,
    string Content,
    bool ShouldLogLargeSave);

internal static class NoteSaveRequestBuilder
{
    internal static NoteSaveRequest? Create(
        string? notePath,
        string content,
        string? openCollectionPath,
        string? currentNotePath,
        string lastSavedContent,
        Func<string, string> getRequiredRelativePath,
        int largeSaveLogThreshold)
    {
        if (string.IsNullOrWhiteSpace(notePath)
            || string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return null;
        }

        string fullNotePath = Path.GetFullPath(notePath);
        string collectionPath = openCollectionPath;
        string relativePath = getRequiredRelativePath(fullNotePath);
        if (IsCurrentSavedNote(currentNotePath, openCollectionPath, fullNotePath, collectionPath)
            && string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return null;
        }

        return new(
            fullNotePath,
            collectionPath,
            relativePath,
            content,
            content.Length >= largeSaveLogThreshold);
    }

    internal static bool IsCurrentSavedNote(
        string? currentNotePath,
        string? openCollectionPath,
        string notePath,
        string collectionPath)
    {
        return !string.IsNullOrWhiteSpace(currentNotePath)
            && string.Equals(Path.GetFullPath(currentNotePath), notePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(openCollectionPath, collectionPath, StringComparison.OrdinalIgnoreCase);
    }
}
