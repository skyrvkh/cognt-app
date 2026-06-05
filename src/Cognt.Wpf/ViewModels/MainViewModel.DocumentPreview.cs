using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void OpenAttachment(KnowledgeAttachmentReadModel attachment)
    {

        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before opening an attachment.");
        }

        if (attachment.Kind == KnowledgeAttachmentKind.Pdf)
        {

            OpenPdfDocument(CreatePdfDocument(attachment));
            return;
        }


        RaiseMessage("Attachment", AttachmentPreviewMessageFactory.CreateUnavailablePreviewMessage(attachment));
    }

    private void OpenPdfDocumentFromTreeNode(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(node.FileRelPath))
        {
            throw new InvalidOperationException("The selected PDF does not have a stored file path.");
        }

        OpenPdfDocumentByRelativePath(node.FileRelPath, node.Id, node.Title);
    }

    private void OpenPdfDocumentByRelativePath(
        string fileRelPath,
        long? treeItemId,
        string? treeTitle)
    {
        string normalizedPath = NormalizeRelativePath(fileRelPath);
        string originalFileName = Path.GetFileName(normalizedPath);
        string title = string.IsNullOrWhiteSpace(treeTitle)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : treeTitle;
        OpenPdfDocument(
            new KnowledgePdfDocumentReadModel(normalizedPath, title),
            treeItemId,
            treeTitle);
    }

    internal bool TryOpenPdfDocumentByRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string normalizedPath = NormalizeRelativePath(relativePath);
        NoteTreeNodeViewModel? loadedPdfNode = GetLoadedTreeNodeByRelativePath(normalizedPath);
        if (loadedPdfNode is { Kind: KnowledgeItemKind.PdfDocument }
            && !string.IsNullOrWhiteSpace(loadedPdfNode.FileRelPath))
        {
            OpenPdfDocumentByRelativePath(loadedPdfNode.FileRelPath, loadedPdfNode.Id, loadedPdfNode.Title);
            return true;
        }

        if (!string.Equals(Path.GetExtension(normalizedPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string title = Path.GetFileNameWithoutExtension(normalizedPath);
        OpenPdfDocument(new KnowledgePdfDocumentReadModel(normalizedPath, title));
        return true;
    }

    private void OpenPdfDocument(
        KnowledgePdfDocumentReadModel document,
        long? treeItemId = null,
        string? treeTitle = null)
    {
        SaveCurrentNoteFromEditor();
        OpenPdfRequested?.Invoke(this, new Navigation.OpenPdfRequestedEventArgs(document));
    }

    internal bool TryGetPdfDocumentByTreeItemId(
        long itemId,
        out KnowledgePdfDocumentReadModel? document)
    {
        document = null;
        if (itemId <= 0 || string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return false;
        }

        KnowledgeTreeItemReadModel? item = noteService.GetItem(openCollectionPath, itemId);
        if (item?.Kind != KnowledgeItemKind.PdfDocument || string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            return false;
        }

        string normalizedPath = NormalizeRelativePath(item.FileRelPath);
        string title = string.IsNullOrWhiteSpace(item.Title)
            ? Path.GetFileNameWithoutExtension(normalizedPath)
            : item.Title;
        document = new KnowledgePdfDocumentReadModel(normalizedPath, title);
        return true;
    }

    internal string ResolveDocumentPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before resolving a stored document path.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("A stored document relative path is required.");
        }

        string resolvedPath = ResolveCollectionFilePath(openCollectionPath, NormalizeRelativePath(relativePath));
        return resolvedPath;
    }

    private static KnowledgePdfDocumentReadModel CreatePdfDocument(KnowledgeAttachmentReadModel attachment)
    {
        return new KnowledgePdfDocumentReadModel(
            attachment.RelativePath,
            Path.GetFileNameWithoutExtension(attachment.OriginalFileName));
    }
}
