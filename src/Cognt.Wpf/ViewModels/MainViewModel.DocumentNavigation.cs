using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal Navigation.PreparedNote? PrepareNoteForOpen(string notePath)
    {
        if (string.Equals(currentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SaveCurrentNoteFromEditor();
        string fullPath = Path.GetFullPath(notePath);
        string relativePath = GetRequiredRelativeNotePath(fullPath);
        KnowledgeOpenedNoteReadModel openedNote = noteService.ReadNoteForOpen(openCollectionPath, relativePath);

        return new Navigation.PreparedNote(
            fullPath,
            openedNote.Title,
            openedNote.Content,
            openedNote.ItemId);
    }

    internal void CompleteOpenNote(Navigation.PreparedNote note)
    {
        currentPdfDocument = null;
        currentNotePath = note.NotePath;
        currentNoteItemId = note.ItemId;
        currentNoteTitle = note.Title;
        lastSavedContent = note.Content;
        ClearNoteToCardTags();
        OnPropertyChanged(nameof(CurrentNotePath));
        RefreshEditorBreadcrumb();
        ReloadCurrentNoteToCardQueue(GetRequiredRelativeNotePath(note.NotePath), note.Content);
    }

    public void ClearCurrentNote()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        ClearEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SaveCurrentNoteFromEditor()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        SaveNoteIfChanged(GetCurrentEditorContent?.Invoke() ?? string.Empty);
    }

    public void SaveNoteIfChanged(string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return;
        }

        noteService.SaveNote(openCollectionPath, GetRequiredCurrentNoteRelativePath(), content);
        lastSavedContent = content;
        RecomputeCardContextDraftStatuses(GetRequiredCurrentNoteRelativePath(), content);
        ReloadCurrentNoteToCardQueue(GetRequiredCurrentNoteRelativePath(), content);
    }

    public void SelectTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null || node.IsPlaceholder)
        {
            ClearCurrentNote();
            return;
        }

        if (node.Kind == KnowledgeItemKind.PdfDocument)
        {
            OpenPdfDocumentFromTreeNode(node);
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.NotePath))
        {
            currentNoteItemId = node.Id;
            RequestOpenResolvedNotePath(node.NotePath, selectInTree: false);
            return;
        }

        SelectFolderNode(node);
    }

    private void SelectFolderNode(NoteTreeNodeViewModel node)
    {
        SaveCurrentNoteFromEditor();

        bool isSameFolder = SelectCurrentFolder(node.Id, node.Title);
        if (!isSameFolder)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectFolderItem(KnowledgeTreeItemReadModel item)
    {
        SaveCurrentNoteFromEditor();

        bool isSameFolder = SelectCurrentFolder(item.Id, item.Title);
        if (!isSameFolder)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool SelectCurrentFolder(long folderId, string title)
    {
        bool isSameFolder = currentNoteItemId == folderId && string.IsNullOrWhiteSpace(currentNotePath);
        currentNotePath = null;
        currentNoteItemId = folderId;
        currentNoteTitle = title;
        lastSavedContent = string.Empty;
        OnPropertyChanged(nameof(CurrentNotePath));
        RefreshEditorBreadcrumb();
        CurrentNoteToCardQueue = null;
        return isSameFolder;
    }

    public bool NavigateToBreadcrumb(Navigation.EditorBreadcrumbItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsCurrent)
        {
            return false;
        }

        KnowledgeTreeItemReadModel treeItem = noteService.GetItem(openCollectionPath, item.Id)
            ?? throw new InvalidOperationException("This breadcrumb item no longer exists.");

        if (treeItem.Kind == KnowledgeItemKind.Note)
        {
            string notePath = item.NotePath ?? ResolveNotePath(treeItem)
                ?? throw new InvalidOperationException("The breadcrumb note file could not be resolved.");
            currentNoteItemId = item.Id;
            RequestOpenResolvedNotePath(notePath, selectInTree: true);
            return true;
        }

        SelectFolderItem(treeItem);
        return true;
    }
}
