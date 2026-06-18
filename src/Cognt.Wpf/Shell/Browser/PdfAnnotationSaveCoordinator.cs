using System.IO;

namespace CogntApp.Shell.Browser;

internal sealed record PdfAnnotationSaveRequest(
    long DownloadId,
    string SourcePath,
    string TemporaryPath,
    string FallbackPath);

internal sealed record PdfAnnotationSaveResult(
    bool ReplacedOriginal,
    string? FallbackPath,
    string? ErrorMessage);

internal static class PdfAnnotationSaveCoordinator
{
    internal static bool TryCreateRequest(
        string? pageAddress,
        long downloadId,
        string collectionPath,
        string downloadsPath,
        out PdfAnnotationSaveRequest? request)
    {
        request = null;
        if (!TryGetLocalPdfPath(pageAddress, out string? sourcePath)
            || !File.Exists(sourcePath)
            || !IsWithinCollection(sourcePath, collectionPath))
        {
            return false;
        }

        string directory = Path.GetDirectoryName(sourcePath)!;
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string temporaryPath = Path.Combine(directory, $".{name}.cognt-annotation-{Guid.NewGuid():N}.tmp.pdf");
        string fallbackPath = CreateFallbackPath(downloadsPath, $"{name}-annotated.pdf");

        request = new PdfAnnotationSaveRequest(downloadId, sourcePath, temporaryPath, fallbackPath);
        return true;
    }

    internal static PdfAnnotationSaveResult Complete(PdfAnnotationSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (!File.Exists(request.TemporaryPath))
            {
                throw new FileNotFoundException("The annotated PDF download was not found.", request.TemporaryPath);
            }

            File.Replace(request.TemporaryPath, request.SourcePath, destinationBackupFileName: null);
            return new PdfAnnotationSaveResult(true, null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.FallbackPath)!);
                File.Move(request.TemporaryPath, request.FallbackPath, overwrite: false);
                return new PdfAnnotationSaveResult(false, request.FallbackPath, ex.Message);
            }
            catch (Exception fallbackException) when (fallbackException is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return new PdfAnnotationSaveResult(false, null, $"{ex.Message} {fallbackException.Message}");
            }
        }
    }

    internal static void Cancel(PdfAnnotationSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (File.Exists(request.TemporaryPath))
        {
            File.Delete(request.TemporaryPath);
        }
    }

    private static bool TryGetLocalPdfPath(string? pageAddress, out string? sourcePath)
    {
        sourcePath = null;
        if (!Uri.TryCreate(pageAddress, UriKind.Absolute, out Uri? uri)
            || !uri.IsFile
            || !string.Equals(Path.GetExtension(uri.LocalPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sourcePath = Path.GetFullPath(uri.LocalPath);
        return true;
    }

    private static bool IsWithinCollection(string sourcePath, string collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            return false;
        }

        string relativePath = Path.GetRelativePath(Path.GetFullPath(collectionPath), sourcePath);
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string CreateFallbackPath(string downloadsPath, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadsPath);
        Directory.CreateDirectory(downloadsPath);

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidatePath = Path.Combine(downloadsPath, fileName);
        int suffix = 2;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(downloadsPath, $"{stem} ({suffix}){extension}");
            suffix++;
        }

        return candidatePath;
    }
}
