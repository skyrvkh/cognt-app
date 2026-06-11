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

    public Task DeleteNodeAsync(NoteTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return DeleteNodesCoreAsync([node]);
    }

    public Task DeleteNodesAsync(IReadOnlyList<NoteTreeNodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        return DeleteNodesCoreAsync(nodes);
    }

    private async Task DeleteNodesCoreAsync(IReadOnlyList<NoteTreeNodeViewModel> nodes)
    {
        NoteTreeNodeViewModel[] deleteNodes = nodes
            .GroupBy(node => node.Id)
            .Select(group => group.First())
            .ToArray();
        if (deleteNodes.Length == 0)
        {
            return;
        }

        foreach (NoteTreeNodeViewModel node in deleteNodes)
        {
            EnsureCanModifyNode(node, "deleted");
        }

        List<string> pendingDeletedNotePaths = [];
        HashSet<string> pendingDeletedPathSet = new(StringComparer.OrdinalIgnoreCase);
        foreach (NoteTreeNodeViewModel node in deleteNodes)
        {
            foreach (string notePath in noteService.PreviewDeleteItemNotePaths(openCollectionPath, node.Id))
            {
                if (pendingDeletedPathSet.Add(notePath))
                {
                    pendingDeletedNotePaths.Add(notePath);
                }
            }
        }

        string[] pendingDeletedFullNotePaths = NoteActionStateBuilder.CreateDeletedFullNotePaths(
            openCollectionPath,
            pendingDeletedNotePaths);
        string? currentNoteRelativePathBeforeClose = TryGetCurrentNoteRelativePath();
        bool deletesCurrentNoteBeforeClose = currentNoteRelativePathBeforeClose is not null
            && NoteActionStateBuilder.ContainsDeletedRelativePath(pendingDeletedNotePaths, currentNoteRelativePathBeforeClose);

        if (!deletesCurrentNoteBeforeClose)
        {
            SaveCurrentNoteFromEditor();
        }

        await RaiseNoteDocumentsDeletingAsync(pendingDeletedFullNotePaths);

        List<string> deletedNotePaths = [];
        HashSet<string> deletedPathSet = new(StringComparer.OrdinalIgnoreCase);
        foreach (NoteTreeNodeViewModel node in deleteNodes)
        {
            foreach (string notePath in noteService.DeleteItem(openCollectionPath, node.Id))
            {
                if (deletedPathSet.Add(notePath))
                {
                    deletedNotePaths.Add(notePath);
                }
            }

            RemoveLoadedTreeNode(node);
        }

        string[] deletedFullNotePaths = NoteActionStateBuilder.CreateDeletedFullNotePaths(
            openCollectionPath,
            deletedNotePaths);

        string? currentNoteRelativePathAfterClose = TryGetCurrentNoteRelativePath();
        bool deletesCurrentNoteAfterClose = currentNoteRelativePathAfterClose is not null
            && NoteActionStateBuilder.ContainsDeletedRelativePath(deletedNotePaths, currentNoteRelativePathAfterClose);
        bool deletesCurrentSelection = currentNoteItemId.HasValue
            && deleteNodes.Any(node => currentNoteItemId == node.Id);

        foreach (string notePath in deletedNotePaths)
        {
            RemoveRecentNote(ResolveCollectionFilePath(openCollectionPath, notePath));
        }

        if (deletesCurrentNoteAfterClose || deletesCurrentSelection)
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

    private async Task RaiseNoteDocumentsDeletingAsync(IReadOnlyList<string> notePaths)
    {
        Func<object?, Navigation.NoteDocumentsDeletedEventArgs, Task>? handlers = NoteDocumentsDeleting;
        if (handlers is null || notePaths.Count == 0)
        {
            return;
        }

        Navigation.NoteDocumentsDeletedEventArgs args = new(notePaths);
        Func<object?, Navigation.NoteDocumentsDeletedEventArgs, Task>[] invocationList = handlers
            .GetInvocationList()
            .Cast<Func<object?, Navigation.NoteDocumentsDeletedEventArgs, Task>>()
            .ToArray();

        foreach (Func<object?, Navigation.NoteDocumentsDeletedEventArgs, Task> handler in invocationList)
        {
            await handler(this, args);
        }
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
        string duplicateTitle = NoteActionStateBuilder.CreateDuplicateNoteTitle(sourceNote.Title);
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

    public void MoveTreeNodes(
        IReadOnlyList<long> itemIds,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        long[] moveItemIds = [.. itemIds.Where(static itemId => itemId > 0).Distinct()];
        if (moveItemIds.Length == 0)
        {
            return;
        }

        if (moveItemIds.Length == 1)
        {
            MoveTreeNode(moveItemIds[0], newParentId, previousSiblingId, nextSiblingId);
            return;
        }

        PrepareLoadedParentForMove(newParentId);

        bool shouldRefreshTree = false;
        long? currentPreviousSiblingId = previousSiblingId;
        long? currentNextSiblingId = nextSiblingId;

        foreach (long itemId in moveItemIds)
        {
            NoteTreeNodeViewModel? node = FindTreeNode(itemId);
            long? oldParentId = node?.ParentId;

            noteService.MoveItem(
                openCollectionPath,
                itemId,
                newParentId,
                currentPreviousSiblingId,
                currentNextSiblingId);

            if (node is not null)
            {
                MoveLoadedTreeNode(node, oldParentId, newParentId, currentPreviousSiblingId, currentNextSiblingId);
            }
            else
            {
                shouldRefreshTree = true;
            }

            currentPreviousSiblingId = itemId;
            currentNextSiblingId = nextSiblingId;
        }

        if (shouldRefreshTree)
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
}
