using System.IO;

namespace CogntApp.ViewModels;

internal static class CollectionPathUtilities
{
    internal static bool TryGetFullPath(string? path, out string fullPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            fullPath = string.Empty;
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
            return true;
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

        fullPath = string.Empty;
        return false;
    }

    internal static string NormalizeDirectoryName(string rawName, string extension)
    {
        string name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Collection name is required.");
        }

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

    internal static string? GetParentPath(string? path)
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

    internal static string FormatName(string? path, string extension)
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

        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? name[..^extension.Length]
            : name;
    }

    internal static bool AreSamePath(string? first, string? second)
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

    internal static bool IsPdfPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
