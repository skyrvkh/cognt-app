using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private const string BlockSuiteAssetsDirectoryName = ".blocksuite-assets";
    private const string BlockSuiteBlobsDirectoryName = "blobs";

    public async Task<KnowledgeAttachmentReadModel> ImportAttachmentAsync(string sourcePath)
    {

        SaveCurrentNoteFromEditor();
        await using FileStream sourceContent = File.OpenRead(sourcePath);
        KnowledgeAttachmentReadModel attachment = await noteService.ImportAttachmentAsync(
            openCollectionPath,
            Path.GetFileName(sourcePath),
            sourceContent);
        OpenAttachment(attachment);

        return attachment;
    }

    public async Task<KnowledgePdfDocumentReadModel> ImportPdfDocumentAsync(
        string sourcePath,
        bool openAfterImport = true)
    {

        SaveCurrentNoteFromEditor();
        await using FileStream sourceContent = File.OpenRead(sourcePath);
        KnowledgePdfDocumentReadModel document = await noteService.ImportPdfDocumentAsync(
            openCollectionPath,
            Path.GetFileName(sourcePath),
            sourceContent);
        RefreshNotesTree(preserveExpansion: true);
        if (openAfterImport)
        {
            OpenPdfDocument(document);
        }


        return document;
    }

    public async Task<string> AttachAttachmentToCurrentNoteAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before attaching a file.");
        }

        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            throw new InvalidOperationException("Open a note before attaching a file.");
        }


        SaveCurrentNoteFromEditor();
        await using FileStream sourceContent = File.OpenRead(sourcePath);
        string attachmentDisplayName = await noteService.AttachFileToNoteAsync(
            openCollectionPath,
            GetRequiredCurrentNoteRelativePath(),
            Path.GetFileName(sourcePath),
            sourceContent,
            cancellationToken);
        if (IsPdfPath(sourcePath))
        {
            RefreshNotesTree(preserveExpansion: true);
        }

        RaiseMessage(
            "Attachment Added",
            $"Attached {attachmentDisplayName} to the current note.");

        return attachmentDisplayName;
    }

    public async Task<KnowledgeAttachmentReadModel> AttachAttachmentStreamToCurrentNoteAsync(
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before attaching a file.");
        }

        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            throw new InvalidOperationException("Open a note before attaching a file.");
        }


        SaveCurrentNoteFromEditor();
        KnowledgeAttachmentReadModel attachment = await AttachAttachmentStreamToNoteCoreAsync(
            currentNotePath,
            sourceFileName,
            sourceContent,
            cancellationToken);

        return attachment;
    }

    public async Task<KnowledgeAttachmentReadModel> AttachAttachmentStreamToNoteAsync(
        string notePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before attaching a file.");
        }

        if (string.IsNullOrWhiteSpace(notePath))
        {
            throw new InvalidOperationException("Open a note before attaching a file.");
        }

        KnowledgeAttachmentReadModel attachment = await AttachAttachmentStreamToNoteCoreAsync(
            notePath,
            sourceFileName,
            sourceContent,
            cancellationToken);

        return attachment;
    }

    private async Task<KnowledgeAttachmentReadModel> AttachAttachmentStreamToNoteCoreAsync(
        string notePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        KnowledgeAttachmentReadModel attachment = await noteService.AttachFileReferenceToNoteAsync(
            openCollectionPath,
            GetRequiredRelativeNotePath(notePath),
            Path.GetFileName(sourceFileName),
            sourceContent,
            cancellationToken);

        if (attachment.Kind == KnowledgeAttachmentKind.Pdf)
        {
            RefreshNotesTree(preserveExpansion: true);
        }


        return attachment;
    }

    public async Task<ReadAttachmentResult> ReadImageAttachmentForCurrentNoteAsync(
        string sourcePath,
        string imageSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before reading an attachment.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("The attachment source is required.");
        }


        string noteRelativePath = GetRequiredRelativeNotePath(sourcePath);
        string imageRelPath = noteService.ResolveImageAttachmentRelativePath(
                openCollectionPath,
                noteRelativePath,
                imageSource)
            ?? throw new InvalidOperationException("The image attachment could not be resolved.");
        string imageFullPath = ResolveCollectionFilePath(openCollectionPath, imageRelPath);
        if (!File.Exists(imageFullPath))
        {
            throw new FileNotFoundException("The image attachment file could not be found.", imageFullPath);
        }

        byte[] bytes = await File.ReadAllBytesAsync(imageFullPath, cancellationToken);

        return ReadAttachmentResult.Success(
            Path.GetFileName(imageFullPath),
            imageRelPath,
            GetImageAttachmentContentType(imageFullPath),
            Convert.ToBase64String(bytes));
    }

    public async Task StoreEditorBlobAsync(
        string sourcePath,
        string blobId,
        string contentType,
        string contentBase64,
        CancellationToken cancellationToken = default)
    {
        string blobFullPath = ResolveEditorBlobPath(sourcePath, blobId, createRoot: true);
        Directory.CreateDirectory(Path.GetDirectoryName(blobFullPath)!);

        byte[] bytes = Convert.FromBase64String(contentBase64);
        await File.WriteAllBytesAsync(blobFullPath, bytes, cancellationToken);
    }

    public async Task<EditorBlobReadResult> ReadEditorBlobAsync(
        string sourcePath,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        string blobFullPath = ResolveEditorBlobPath(sourcePath, blobId, createRoot: false);
        if (!File.Exists(blobFullPath))
        {
            return EditorBlobReadResult.Fail("The editor blob could not be found.");
        }

        byte[] bytes = await File.ReadAllBytesAsync(blobFullPath, cancellationToken);
        string contentType = GetEditorBlobContentType(blobFullPath, bytes);
        return EditorBlobReadResult.Success(contentType, Convert.ToBase64String(bytes));
    }

    public IReadOnlyList<string> ListEditorBlobIds(string sourcePath)
    {
        string blobRoot = ResolveEditorBlobRoot(sourcePath, createRoot: false);
        if (!Directory.Exists(blobRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(blobRoot, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(blobRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetImageAttachmentContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }

    private string ResolveEditorBlobPath(string sourcePath, string blobId, bool createRoot)
    {
        string blobRoot = ResolveEditorBlobRoot(sourcePath, createRoot);
        string normalizedBlobId = NormalizeEditorBlobId(blobId);
        string blobFullPath = Path.GetFullPath(Path.Combine(
            blobRoot,
            normalizedBlobId.Replace('/', Path.DirectorySeparatorChar)));

        if (!IsPathInsideRoot(blobFullPath, blobRoot))
        {
            throw new UnauthorizedAccessException("The editor blob path is outside the collection asset store.");
        }

        return blobFullPath;
    }

    private string ResolveEditorBlobRoot(string sourcePath, bool createRoot)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before accessing editor blobs.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("The editor blob source is required.");
        }

        _ = GetRequiredRelativeNotePath(sourcePath);

        string assetRoot = Path.GetFullPath(Path.Combine(openCollectionPath, BlockSuiteAssetsDirectoryName));
        string blobRoot = Path.GetFullPath(Path.Combine(assetRoot, BlockSuiteBlobsDirectoryName));
        if (!IsPathInsideRoot(blobRoot, Path.GetFullPath(openCollectionPath)))
        {
            throw new UnauthorizedAccessException("The editor blob store is outside the open collection root.");
        }

        if (createRoot)
        {
            Directory.CreateDirectory(blobRoot);
            TryMarkDirectoryHidden(assetRoot);
        }

        return blobRoot;
    }

    private static string NormalizeEditorBlobId(string blobId)
    {
        string normalized = blobId.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized)
            || Uri.TryCreate(normalized, UriKind.Absolute, out _)
            || Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException("The editor blob id is invalid.");
        }

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (string segment in normalized.Split('/'))
        {
            if (string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || segment.IndexOfAny(invalidFileNameChars) >= 0)
            {
                throw new InvalidOperationException("The editor blob id is invalid.");
            }
        }

        return normalized;
    }

    private static bool IsPathInsideRoot(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootWithSeparator = string.Concat(fullRoot, Path.DirectorySeparatorChar);

        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryMarkDirectoryHidden(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Hidden) == 0)
            {
                File.SetAttributes(path, attributes | FileAttributes.Hidden);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetEditorBlobContentType(string path, byte[] bytes)
    {
        string extensionType = GetImageAttachmentContentType(path);
        if (extensionType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return extensionType;
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x38
            && (bytes[4] == 0x37 || bytes[4] == 0x39)
            && bytes[5] == 0x61)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return "application/octet-stream";
    }
}
