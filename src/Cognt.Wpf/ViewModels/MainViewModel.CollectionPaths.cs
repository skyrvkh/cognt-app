using System.IO;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private Dictionary<string, string> collectionPathsById = new(StringComparer.OrdinalIgnoreCase);
    private void AddCollectionPath(SortedSet<string> paths, string path, bool includeHidden = false)
    {
        if (TryNormalizeExistingCollectionPath(path, out string? fullPath))
        {
            if (!includeHidden && knownCollectionStore.IsCollectionPathHidden(fullPath))
            {
                return;
            }

            editorSettingsService.EnsureCollectionSettingsFile(fullPath);
            paths.Add(fullPath);
            return;
        }

    }

    private bool IsExistingCollectionPath(string path)
        => TryNormalizeExistingCollectionPath(path, out _);

    private bool TryNormalizeCollectionActionPath(object? parameter, out string fullPath)
    {
        string? path = parameter switch
        {
            Navigation.CollectionOptionViewModel collection => collection.Path,
            string text => text,
            null => CurrentCollectionPath,
            _ => null
        };

        if (!CollectionPathUtilities.TryGetFullPath(path, out fullPath))
        {
            return false;
        }

        return collectionPathResolver.IsCollectionPath(fullPath);
    }

    private bool TryNormalizeExistingCollectionPath(string path, out string fullPath)
    {
        if (!CollectionPathUtilities.TryGetFullPath(path, out fullPath))
        {
            return false;
        }

        bool isValid = collectionPathResolver.IsCollectionPath(fullPath) && Directory.Exists(fullPath);
        return isValid;
    }

    private string? ResolveCollectionPathFromId(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return null;
        }

        if (collectionPathsById.TryGetValue(collectionId, out string? collectionPath) && Directory.Exists(collectionPath))
        {
            return collectionPath;
        }

        if (Directory.Exists(collectionId))
        {
            string fullPath = Path.GetFullPath(collectionId);
            return fullPath;
        }

        return null;
    }

    private string ResolveCollectionCreationParentPath()
    {
        string? parentPath = GetCollectionParentPath(CurrentCollectionPath)
            ?? GetCollectionParentPath(CollectionPath)
            ?? GetCollectionParentPath(collectionPathResolver.ResolveDefaultCollectionPath())
            ?? Environment.CurrentDirectory;

        Directory.CreateDirectory(parentPath);
        return parentPath;
    }

    internal string GetDefaultCollectionCreationParentPath()
    {
        return ResolveCollectionCreationParentPath();
    }

    private string NormalizeCollectionDirectoryName(string rawName)
        => CollectionPathUtilities.NormalizeDirectoryName(
            rawName,
            collectionPathResolver.CollectionDirectoryExtension);

    private static string? GetCollectionParentPath(string path)
        => CollectionPathUtilities.GetParentPath(path);

    private string FormatCollectionName(string path)
        => CollectionPathUtilities.FormatName(
            path,
            collectionPathResolver.CollectionDirectoryExtension);

    private static bool AreSamePath(string first, string second)
        => CollectionPathUtilities.AreSamePath(first, second);

    private static bool IsPdfPath(string path)
        => CollectionPathUtilities.IsPdfPath(path);
}
