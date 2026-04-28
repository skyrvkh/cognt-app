using System.IO;
using System.Windows;

namespace CogntApp.Shell.Attachments;

internal static class AttachmentImportPolicy
{
    internal const string PdfImportKind = "Pdf";

    private const string ImageImportKind = "Image";
    private const string PdfExtension = ".pdf";

    private static readonly string[] DocumentExtensions =
    [
        ".docx",
        ".doc",
        ".rtf",
        ".txt",
        ".md",
        ".markdown"
    ];

    private static readonly string[] ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    ];

    internal static string GetDialogFilter(string importKind)
    {
        return importKind switch
        {
            PdfImportKind => "PDF files (*.pdf)|*.pdf",
            ImageImportKind => "Image files (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif",
            _ => "Document files (*.docx;*.doc;*.rtf;*.txt;*.md;*.markdown)|*.docx;*.doc;*.rtf;*.txt;*.md;*.markdown"
        };
    }

    internal static IReadOnlyList<string> GetDroppedAttachmentPaths(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return [];
        }

        return paths
            .Where(IsSupportedPath)
            .ToArray();
    }

    private static bool IsSupportedPath(string path)
    {
        string extension = Path.GetExtension(path);
        return IsSupportedExtension(extension, PdfExtension)
            || IsSupportedExtension(extension, DocumentExtensions)
            || IsSupportedExtension(extension, ImageExtensions);
    }

    private static bool IsSupportedExtension(string extension, string supportedExtension)
    {
        return extension.Equals(supportedExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedExtension(string extension, IEnumerable<string> supportedExtensions)
    {
        return supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
