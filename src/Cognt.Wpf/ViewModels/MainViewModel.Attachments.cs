using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
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
        KnowledgeAttachmentReadModel attachment = await noteService.AttachFileReferenceToNoteAsync(
            openCollectionPath,
            GetRequiredCurrentNoteRelativePath(),
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
}
