using System.IO;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void AddCollectionPath(SortedSet<string> paths, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
            if (collectionPathResolver.IsCollectionPath(fullPath) && Directory.Exists(fullPath))
            {
                paths.Add(fullPath);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private string? ResolveCollectionPathFromId(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return null;
        }

        Navigation.CollectionOptionViewModel? collection = AvailableCollections.FirstOrDefault(
            option => string.Equals(option.Id, collectionId, StringComparison.OrdinalIgnoreCase));
        if (collection is not null && Directory.Exists(collection.Path))
        {
            return collection.Path;
        }

        return Directory.Exists(collectionId)
            ? Path.GetFullPath(collectionId)
            : null;
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
    {
        string name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Collection name is required.");
        }

        string extension = collectionPathResolver.CollectionDirectoryExtension;
        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length].TrimEnd();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Collection name is required.");
        }

        if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Collection name contains invalid file name characters.");
        }

        if (name.EndsWith(".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Collection name cannot end with a period.");
        }

        return $"{name}{extension}";
    }

    private static string? GetCollectionParentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Directory.GetParent(Path.GetFullPath(path))?.FullName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string FormatCollectionName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "No Collection";
        }

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (string.IsNullOrWhiteSpace(name))
        {
            return "No Collection";
        }

        string extension = collectionPathResolver.CollectionDirectoryExtension;
        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? name[..^extension.Length]
            : name;
    }

    private static bool AreSamePath(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
        catch (NotSupportedException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsPdfPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
