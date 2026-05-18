using System.IO;

namespace CogntApp.Shell.DocumentImport;

public enum DocumentImportType
{
    Docx,
    Pdf,
    Markdown
}

public enum DocumentImportMode
{
    ConvertToNativeDocument,
    ImportAsPdf
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
    private static readonly string[] SupportedExtensions = [".docx", ".pdf", ".md"];

    internal static bool IsSupportedPath(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    internal static DocumentImportType GetImportType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" => DocumentImportType.Docx,
            ".pdf" => DocumentImportType.Pdf,
            ".md" => DocumentImportType.Markdown,
            string extension => throw new InvalidOperationException(
                $"Unsupported file type \"{extension}\". Add a DOCX, PDF, or MD file.")
        };
    }

    internal static DocumentImportMode GetImportMode(DocumentImportType type)
    {
        return type switch
        {
            DocumentImportType.Pdf => DocumentImportMode.ImportAsPdf,
            _ => DocumentImportMode.ConvertToNativeDocument
        };
    }

    internal static string GetTypeLabel(DocumentImportType type)
    {
        return type switch
        {
            DocumentImportType.Docx => "DOCX",
            DocumentImportType.Pdf => "PDF",
            DocumentImportType.Markdown => "MD",
            _ => "FILE"
        };
    }

    internal static string GetModeLabel(DocumentImportMode mode)
    {
        return mode switch
        {
            DocumentImportMode.ImportAsPdf => "Import as PDF",
            _ => "Convert to editable note"
        };
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
