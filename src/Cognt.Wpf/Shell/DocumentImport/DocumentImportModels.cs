using System.IO;

namespace CogntApp.Shell.DocumentImport;

public enum DocumentImportType
{
    Markdown
}

public enum DocumentImportMode
{
    ConvertToNativeDocument
}

public enum DocumentImportStatus
{
    Ready,
    Importing,
    Success,
    Failed
}

internal sealed record DocumentImportOptions(bool OpenImportedDocumentAfterImport);

internal static class DocumentImportPolicy
{
    private static readonly string[] SupportedExtensions = [".md", ".markdown"];

    internal static bool IsSupportedPath(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    internal static DocumentImportType GetImportType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => DocumentImportType.Markdown,
            string extension => throw new InvalidOperationException(
                $"Unsupported file type \"{extension}\". Add a Markdown file.")
        };
    }

    internal static DocumentImportMode GetImportMode(DocumentImportType type)
    {
        return DocumentImportMode.ConvertToNativeDocument;
    }

    internal static string GetTypeLabel(DocumentImportType type)
    {
        return "MD";
    }

    internal static string GetModeLabel(DocumentImportMode mode)
    {
        return "Convert to native document";
    }

    internal static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }
}
