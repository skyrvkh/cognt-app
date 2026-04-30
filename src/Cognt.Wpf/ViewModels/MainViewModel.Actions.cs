using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void RenameBreadcrumbItem(Navigation.EditorBreadcrumbItemViewModel item, string newName)
    {
        EnsureBreadcrumbCanBeRenamed(item, newName);

        NoteTreeNodeViewModel? loadedNode = FindTreeNode(item.Id);
        if (loadedNode is not null)
        {
            RenameNode(loadedNode, newName);
            item.CommitTitle(newName);
            return;
        }

        KnowledgeTreeItemReadModel? treeItem = noteService.GetItem(openCollectionPath, item.Id)
            ?? throw new InvalidOperationException("This breadcrumb item no longer exists.");

        noteService.RenameItem(openCollectionPath, item.Id, newName);

        if (currentNoteItemId == item.Id)
        {
            currentNoteTitle = newName;
            CurrentNoteTitleChanged?.Invoke(this, currentNoteTitle);
        }

        UpdateRecentNoteTitle(ResolveNotePath(treeItem), newName);
        item.CommitTitle(newName);
        RefreshEditorBreadcrumb();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RenameNode(NoteTreeNodeViewModel node, string newName)
    {
        EnsureCanModifyNode(node, "renamed");

        noteService.RenameItem(openCollectionPath, node.Id, newName);
        node.UpdateTitle(newName);

        if (currentNoteItemId == node.Id)
        {
            currentNoteTitle = newName;
            CurrentNoteTitleChanged?.Invoke(this, currentNoteTitle);
        }

        UpdateRecentNoteTitle(node.NotePath, newName);
        RefreshEditorBreadcrumb();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNode(NoteTreeNodeViewModel node)
    {
        EnsureCanModifyNode(node, "deleted");
        SaveCurrentNoteFromEditor();

        IReadOnlyList<string> deletedNotePaths = noteService.DeleteItem(openCollectionPath, node.Id);
        string? currentNoteRelativePath = TryGetCurrentNoteRelativePath();
        bool deletesCurrentNote = currentNoteRelativePath is not null
            && deletedNotePaths.Any(path => string.Equals(
                NormalizeRelativePath(path),
                currentNoteRelativePath,
                StringComparison.OrdinalIgnoreCase));
        bool deletesCurrentSelection = currentNoteItemId == node.Id;

        RemoveLoadedTreeNode(node);
        foreach (string notePath in deletedNotePaths)
        {
            RemoveRecentNote(ResolveCollectionFilePath(openCollectionPath, notePath));
        }

        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (deletesCurrentNote || deletesCurrentSelection)
        {
            ClearCurrentNoteState();
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public string CreateFolder(NoteTreeNodeViewModel? node, string folderName)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        long folderId = noteService.CreateFolder(
            openCollectionPath,
            GetFolderCreationParentId(node),
            folderName);

        AddLoadedTreeNode(noteService.GetItem(openCollectionPath, folderId));
        return folderId.ToString();
    }

    public void MoveTreeNode(
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        NoteTreeNodeViewModel? node = FindTreeNode(itemId);
        long? oldParentId = node?.ParentId;

        PrepareLoadedParentForMove(newParentId);

        noteService.MoveItem(
            openCollectionPath,
            itemId,
            newParentId,
            previousSiblingId,
            nextSiblingId);

        if (node is not null)
        {
            MoveLoadedTreeNode(node, oldParentId, newParentId, previousSiblingId, nextSiblingId);
        }
        else
        {
            RefreshNotesTree(preserveExpansion: true);
        }

        RefreshEditorBreadcrumb();
    }

    private void ShowAllNotes()
    {
        SaveCurrentNoteFromEditor();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.AllNotes));
    }

    private void ShowDecks()
    {
        SaveCurrentNoteFromEditor();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Decks));
    }

    private void ShowSettings()
    {
        SaveCurrentNoteFromEditor();
        RefreshReviewSettings();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Settings));
    }
}
