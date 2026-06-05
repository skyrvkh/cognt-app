using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public string CreateExtractNote(string? sourcePath, string selectedContent, string selectedPlainText)
    {
        ExtractNoteCreationRequest request = PrepareExtractNoteCreation(sourcePath);
        KnowledgeCreatedNoteReadModel createdNote = CreateExtractNoteCore(
            request,
            selectedContent,
            selectedPlainText);
        return CompleteExtractNoteCreation(createdNote);
    }

    public async Task<string> CreateExtractNoteAsync(
        string? sourcePath,
        string selectedContent,
        string selectedPlainText)
    {
        ExtractNoteCreationRequest request = PrepareExtractNoteCreation(sourcePath);
        KnowledgeCreatedNoteReadModel createdNote = await Task.Run(() => CreateExtractNoteCore(
            request,
            selectedContent,
            selectedPlainText));
        return CompleteExtractNoteCreation(createdNote);
    }

    private ExtractNoteCreationRequest PrepareExtractNoteCreation(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The extract source is not the current note file.");
        }

        long? parentId = currentNoteItemId;
        NoteTreeNodeViewModel? parentNode = parentId.HasValue
            ? FindTreeNode(parentId.Value)
            : null;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        return new ExtractNoteCreationRequest(
            openCollectionPath,
            parentId,
            GetRequiredRelativeNotePath(sourcePath),
            "Extract");
    }

    private KnowledgeCreatedNoteReadModel CreateExtractNoteCore(
        ExtractNoteCreationRequest request,
        string selectedContent,
        string selectedPlainText)
    {
        string content = CreateExtractContent(selectedContent, selectedPlainText, request.Title);
        return noteService.CreateExtractNote(
            request.CollectionPath,
            request.ParentId,
            request.SourceRelativePath,
            content);
    }

    private string CompleteExtractNoteCreation(KnowledgeCreatedNoteReadModel createdNote)
    {
        ApplyCreatedNoteState(createdNote);
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return createdNote.RelativePath;
    }

    private string CreateExtractContent(string selectedContent, string selectedPlainText, string title)
    {
        string? normalizedBlockSuiteContent = ExtractNoteContentNormalizer.NormalizeBlockSuiteContent(selectedContent);
        if (!string.IsNullOrWhiteSpace(normalizedBlockSuiteContent))
        {
            return normalizedBlockSuiteContent;
        }

        if (string.IsNullOrWhiteSpace(selectedPlainText))
        {
            throw new InvalidOperationException("The selected content is empty.");
        }

        return documentContentFactory.FromPlainText(selectedPlainText, title);
    }

    public CreateLinkedNoteResult CreateLinkedNote(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The linked note source is not the current note file.");
        }

        NoteTreeNodeViewModel? parentNode = currentNoteItemId.HasValue
            ? FindTreeNode(currentNoteItemId.Value)
            : null;

        long? parentId = parentNode is { CanContainChildren: true }
            ? parentNode.Id
            : parentNode?.ParentId;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Untitled";
        string documentContent = documentContentFactory.FromPlainText(string.Empty, title);
        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            parentId,
            title,
            documentContent);

        ApplyCreatedNoteState(createdNote);
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return CreateLinkedNoteResult.Success(createdNote.RelativePath, title, documentContent);
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return;
        }

        KnowledgeCreatedNoteReadModel createdNote = CreateNoteCore(parentId: null, "Untitled");
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
    }

    public string CreateNote(NoteTreeNodeViewModel? node, string title)
    {
        KnowledgeCreatedNoteReadModel createdNote = CreateNoteCore(GetFolderCreationParentId(node), title);
        return createdNote.ItemId.ToString();
    }

    private KnowledgeCreatedNoteReadModel CreateNoteCore(long? parentId, string title)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Title is required.");
        }

        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            parentId,
            title,
            documentContentFactory.FromPlainText(string.Empty, title));
        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return createdNote;
    }

    private void ApplyCreatedNoteState(
        KnowledgeCreatedNoteReadModel createdNote,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        string notePath = ResolveCollectionFilePath(openCollectionPath, createdNote.RelativePath);
        KnowledgeTreeItemReadModel item = new(
            createdNote.ItemId,
            createdNote.ParentId,
            KnowledgeItemKind.Note,
            createdNote.Title,
            createdNote.RelativePath,
            0);
        AddLoadedTreeNode(item, previousSiblingId, nextSiblingId);
        AddOrUpdateRecentNote(item, notePath);
    }

    private readonly record struct ExtractNoteCreationRequest(
        string CollectionPath,
        long? ParentId,
        string SourceRelativePath,
        string Title);
}
