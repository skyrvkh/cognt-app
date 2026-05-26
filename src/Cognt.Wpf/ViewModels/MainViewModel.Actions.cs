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

    public void RenameNoteItem(long itemId, string newName)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("Name is required.");
        }

        NoteTreeNodeViewModel? loadedNode = FindTreeNode(itemId);
        if (loadedNode is not null)
        {
            if (string.Equals(newName, loadedNode.Title, StringComparison.Ordinal))
            {
                return;
            }

            RenameNode(loadedNode, newName);
            return;
        }

        KnowledgeTreeItemReadModel? treeItem = noteService.GetItem(openCollectionPath, itemId)
            ?? throw new InvalidOperationException("This note no longer exists.");

        if (string.Equals(newName, treeItem.Title, StringComparison.Ordinal))
        {
            return;
        }

        noteService.RenameItem(openCollectionPath, itemId, newName);

        if (currentNoteItemId == itemId)
        {
            currentNoteTitle = newName;
            CurrentNoteTitleChanged?.Invoke(this, currentNoteTitle);
        }

        UpdateRecentNoteTitle(ResolveNotePath(treeItem), newName);
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
        string[] deletedFullNotePaths = CreateDeletedFullNotePaths(deletedNotePaths);
        string? currentNoteRelativePath = TryGetCurrentNoteRelativePath();
        bool deletesCurrentNote = currentNoteRelativePath is not null
            && ContainsDeletedRelativePath(deletedNotePaths, currentNoteRelativePath);
        bool deletesCurrentSelection = currentNoteItemId == node.Id;

        RemoveLoadedTreeNode(node);
        foreach (string notePath in deletedNotePaths)
        {
            RemoveRecentNote(ResolveCollectionFilePath(openCollectionPath, notePath));
        }

        if (deletesCurrentNote || deletesCurrentSelection)
        {
            ClearCurrentNoteState();
        }

        if (deletedFullNotePaths.Length > 0)
        {
            NoteDocumentsDeleted?.Invoke(this, new Navigation.NoteDocumentsDeletedEventArgs(deletedFullNotePaths));
        }

        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private string[] CreateDeletedFullNotePaths(IReadOnlyList<string> deletedNotePaths)
    {
        HashSet<string> seenFullPaths = new(StringComparer.OrdinalIgnoreCase);
        List<string> fullPaths = new(deletedNotePaths.Count);
        foreach (string notePath in deletedNotePaths)
        {
            string fullPath = Path.GetFullPath(ResolveCollectionFilePath(openCollectionPath, notePath));
            if (seenFullPaths.Add(fullPath))
            {
                fullPaths.Add(fullPath);
            }
        }

        return fullPaths.ToArray();
    }

    private static bool ContainsDeletedRelativePath(
        IReadOnlyList<string> deletedNotePaths,
        string currentNoteRelativePath)
    {
        foreach (string notePath in deletedNotePaths)
        {
            if (string.Equals(
                NormalizeRelativePath(notePath),
                currentNoteRelativePath,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string DuplicateNote(NoteTreeNodeViewModel node)
    {
        if (!CanDuplicateNode(node))
        {
            throw new InvalidOperationException("Only notes can be duplicated.");
        }

        SaveCurrentNoteFromEditor();

        string sourceRelativePath = !string.IsNullOrWhiteSpace(node.FileRelPath)
            ? NormalizeRelativePath(node.FileRelPath)
            : GetRequiredRelativeNotePath(node.NotePath);
        KnowledgeOpenedNoteReadModel sourceNote = noteService.ReadNoteForOpen(openCollectionPath, sourceRelativePath);
        string duplicateTitle = CreateDuplicateNoteTitle(sourceNote.Title);
        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            node.ParentId,
            duplicateTitle,
            sourceNote.Content,
            previousSiblingId: node.Id);

        ApplyCreatedNoteState(createdNote, previousSiblingId: node.Id);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
        return createdNote.RelativePath;
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

    private void ShowGraph()
    {
        SaveCurrentNoteFromEditor();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Graph));
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
        RefreshGraphSettings();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Settings));
    }

    private static string CreateDuplicateNoteTitle(string title)
    {
        string trimmedTitle = string.IsNullOrWhiteSpace(title)
            ? "Untitled"
            : title.Trim();
        return $"{trimmedTitle} Copy";
    }
}
