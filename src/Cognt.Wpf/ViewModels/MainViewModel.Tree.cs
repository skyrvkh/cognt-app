using System.Collections.ObjectModel;
using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using CogntApp.Models.Navigation;
using CogntApp.Models.Tree;
using KnowledgeItemKind = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.KnowledgeItemKind;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public bool CanRenameNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public bool CanDeleteNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public string GetEditableName(NoteTreeNodeViewModel node)
    {
        return node.Title;
    }

    public string GetNodeKind(NoteTreeNodeViewModel node)
    {
        return node.Kind is KnowledgeItemKind.Note or KnowledgeItemKind.PdfDocument ? "note" : "folder";
    }

    public void RenameNode(NoteTreeNodeViewModel node, string newName)
    {
        EnsureCanModifyNode(node, "renamed");

        noteService.RenameItem(currentNotesDirectory, node.Id, newName);
        node.UpdateTitle(newName);

        if (currentNoteItemId == node.Id)
        {
            currentNoteTitle = newName;
            CurrentNoteTitleChanged?.Invoke(this, currentNoteTitle);
        }

        UpdateRecentNoteTitle(node.NotePath, newName);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNode(NoteTreeNodeViewModel node)
    {
        EnsureCanModifyNode(node, "deleted");
        SaveCurrentNoteFromEditor();

        string? currentNoteFullPath = string.IsNullOrWhiteSpace(currentNotePath)
            ? null
            : Path.GetFullPath(currentNotePath);

        IReadOnlyList<string> deletedNotePaths = noteService.DeleteItem(currentNotesDirectory, node.Id);
        bool deletesCurrentNote = currentNoteFullPath is not null
            && deletedNotePaths.Any(path => string.Equals(
                Path.GetFullPath(path),
                currentNoteFullPath,
                StringComparison.OrdinalIgnoreCase));

        RemoveLoadedTreeNode(node);
        foreach (string notePath in deletedNotePaths)
        {
            RemoveRecentNote(notePath);
        }
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (deletesCurrentNote)
        {
            ClearCurrentNoteState();
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<string> ImportDocuments(IEnumerable<string> sourcePaths)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        SaveCurrentNoteFromEditor();
        IReadOnlyList<string> importedPaths = noteService.ImportNoteFiles(currentNotesDirectory, sourcePaths);

        if (importedPaths.Count == 0)
        {
            return importedPaths;
        }

        foreach (string importedPath in importedPaths)
        {
            TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, importedPath);
            AddLoadedTreeNode(item);
            AddOrUpdateRecentNote(item, importedPath);
        }

        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(importedPaths[0], SelectInTree: true));

        return importedPaths;
    }

    public bool CanCreateFolderInNode(NoteTreeNodeViewModel? node)
    {
        return !string.IsNullOrWhiteSpace(currentNotesDirectory)
            && (node is null || node.CanContainChildren);
    }

    public string CreateFolder(NoteTreeNodeViewModel? node, string folderName)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        long folderId = noteService.CreateFolder(
            currentNotesDirectory,
            GetFolderCreationParentId(node),
            folderName);

        AddLoadedTreeNode(noteService.GetItem(currentNotesDirectory, folderId));
        return folderId.ToString();
    }

    public bool CanMoveNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
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
            currentNotesDirectory,
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
    }

    private bool CanModifyNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
    }

    private static long? GetFolderCreationParentId(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.CanContainChildren)
        {
            return node.Id;
        }

        return node.ParentId;
    }

    private void EnsureCanModifyNode(NoteTreeNodeViewModel node, string operation)
    {
        if (!CanModifyNode(node))
        {
            throw new InvalidOperationException($"This item cannot be {operation}.");
        }
    }

    private void RefreshNotesTree(bool preserveExpansion = false)
    {
        HashSet<long> expandedItemIds = preserveExpansion
            ? CaptureExpandedItemIds()
            : [];

        IsLoadingNotesDirectory = true;

        try
        {
            NoteRoots.Clear();

            if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
            {
                return;
            }

            List<string> noteFiles = EnumerateNoteFiles(currentNotesDirectory).ToList();

            PopulateRecentNotes(noteFiles);

            foreach (TreeItemRow item in noteService.LoadRootItems(currentNotesDirectory))
            {
                NoteRoots.Add(CreateTreeNode(item));
            }

            if (expandedItemIds.Count > 0)
            {
                RestoreExpandedNodes(NoteRoots, expandedItemIds);
            }
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            NoteRoots.Clear();
            RecentNotes.Clear();
            RaiseMessage("Collection Load Failed", $"The collection could not be read: {ex.Message}");
        }
        finally
        {
            IsLoadingNotesDirectory = false;
        }
    }

    private void AddOrUpdateRecentNote(TreeItemRow? item, string? notePath)
    {
        if (item is null
            || item.Kind is not (TreeItemKind.Note or TreeItemKind.PdfDocument)
            || string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        RemoveRecentNote(notePath);
        RecentNotes.Insert(0, new RecentNoteViewModel(item.Title, Path.GetFullPath(notePath)));

        while (RecentNotes.Count > 5)
        {
            RecentNotes.RemoveAt(RecentNotes.Count - 1);
        }
    }

    private void UpdateRecentNoteTitle(string? notePath, string title)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = 0; index < RecentNotes.Count; index++)
        {
            RecentNoteViewModel recentNote = RecentNotes[index];
            if (string.Equals(
                    Path.GetFullPath(recentNote.NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes[index] = recentNote with { DisplayName = title };
                return;
            }
        }
    }

    private void RemoveRecentNote(string? notePath)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = RecentNotes.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                    Path.GetFullPath(RecentNotes[index].NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes.RemoveAt(index);
            }
        }
    }

    private void PopulateRecentNotes(IEnumerable<string> noteFiles)
    {
        RecentNotes.Clear();

        foreach (string filePath in noteFiles
                     .OrderByDescending(File.GetLastWriteTime)
                     .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            TreeItemRow? noteItem = TryFindNoteMetadata(filePath);
            if (noteItem is null)
            {
                continue;
            }

            RecentNotes.Add(new RecentNoteViewModel(
                noteItem.Title,
                filePath));

            if (RecentNotes.Count >= 5)
            {
                break;
            }
        }
    }

    private TreeItemRow? TryFindNoteMetadata(string notePath)
    {
        try
        {
            return noteService.FindNoteByPath(currentNotesDirectory, notePath);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            return null;
        }
    }

    public void LoadChildren(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory)
            || node.IsPlaceholder
            || !node.CanContainChildren
            || node.AreChildrenLoaded)
        {
            return;
        }

        node.Children.Clear();

        foreach (TreeItemRow child in noteService.LoadChildren(currentNotesDirectory, node.Id))
        {
            node.Children.Add(CreateTreeNode(child));
        }

        node.MarkChildrenLoaded();
    }

    private NoteTreeNodeViewModel CreateTreeNode(TreeItemRow item)
    {
        KnowledgeTreeItemRow knowledgeItem = ToKnowledgeTreeItemRow(item);
        KnowledgeItemKind itemKind = ToKnowledgeItemKind(knowledgeItem.Kind);
        string? notePath = itemKind is KnowledgeItemKind.Note or KnowledgeItemKind.PdfDocument
            && !string.IsNullOrWhiteSpace(knowledgeItem.FileRelPath)
            ? Path.Combine(currentNotesDirectory, knowledgeItem.FileRelPath.Replace('/', Path.DirectorySeparatorChar))
            : null;

        NoteTreeNodeViewModel node = new(
            knowledgeItem.Id,
            knowledgeItem.ParentId,
            itemKind,
            knowledgeItem.Title,
            knowledgeItem.FileRelPath,
            notePath,
            knowledgeItem.ChildCount)
        {
            IsExpanded = false,
        };

        if (item.ChildCount > 0 && node.CanContainChildren)
        {
            node.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }

        return node;
    }

    private static KnowledgeTreeItemRow ToKnowledgeTreeItemRow(TreeItemRow item)
    {
        return new KnowledgeTreeItemRow(
            item.Id,
            item.Uid,
            item.ParentId,
            item.Kind switch
            {
                TreeItemKind.Root => KnowledgeTreeItemKind.Root,
                TreeItemKind.Trash => KnowledgeTreeItemKind.Trash,
                TreeItemKind.Note => KnowledgeTreeItemKind.Note,
                TreeItemKind.PdfDocument => KnowledgeTreeItemKind.PdfDocument,
                _ => KnowledgeTreeItemKind.Folder,
            },
            item.Title,
            item.Position,
            item.FileRelPath,
            item.ChildCount,
            item.IsDeleted,
            item.CreatedAt,
            item.UpdatedAt,
            item.FileSize,
            item.FileMtimeUtc,
            item.ContentHash);
    }

    private static KnowledgeItemKind ToKnowledgeItemKind(KnowledgeTreeItemKind kind)
    {
        return kind switch
        {
            KnowledgeTreeItemKind.Root => KnowledgeItemKind.Root,
            KnowledgeTreeItemKind.Trash => KnowledgeItemKind.Trash,
            KnowledgeTreeItemKind.Note => KnowledgeItemKind.Note,
            KnowledgeTreeItemKind.PdfDocument => KnowledgeItemKind.PdfDocument,
            _ => KnowledgeItemKind.Folder,
        };
    }

    private void AddLoadedTreeNode(TreeItemRow? item)
    {
        if (item is null)
        {
            RefreshNotesTree(preserveExpansion: true);
            return;
        }

        NoteTreeNodeViewModel node = CreateTreeNode(item);
        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindLoadedSiblingCollection(item.ParentId);

        if (siblings is not null)
        {
            InsertNodeBySiblings(siblings, node, previousSiblingId: null, nextSiblingId: null);
        }

        UpdateParentChildCount(item.ParentId, delta: 1);
    }

    private void RemoveLoadedTreeNode(NoteTreeNodeViewModel node)
    {
        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindOwningCollection(node);

        if (siblings is not null)
        {
            siblings.Remove(node);
        }

        UpdateParentChildCount(node.ParentId, delta: -1);
    }

    private void MoveLoadedTreeNode(
        NoteTreeNodeViewModel node,
        long? oldParentId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ObservableCollection<NoteTreeNodeViewModel>? oldSiblings = FindOwningCollection(node);

        if (oldSiblings is not null)
        {
            oldSiblings.Remove(node);
        }

        node.UpdateParent(newParentId);

        ObservableCollection<NoteTreeNodeViewModel>? newSiblings = FindLoadedSiblingCollection(newParentId);
        if (newSiblings is not null)
        {
            RemoveNodeById(newSiblings, node.Id);
            InsertNodeBySiblings(newSiblings, node, previousSiblingId, nextSiblingId);
        }

        if (oldParentId != newParentId)
        {
            UpdateParentChildCount(oldParentId, delta: -1);
            UpdateParentChildCount(newParentId, delta: 1);
        }
    }

    private void PrepareLoadedParentForMove(long? parentId)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        if (parent is not { CanContainChildren: true })
        {
            return;
        }

        if (!parent.AreChildrenLoaded)
        {
            LoadChildren(parent);
        }

        parent.IsExpanded = true;
    }

    private void InsertNodeBySiblings(
        ObservableCollection<NoteTreeNodeViewModel> siblings,
        NoteTreeNodeViewModel node,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        RemovePlaceholders(siblings);

        int insertIndex = siblings.Count;

        if (previousSiblingId.HasValue)
        {
            int previousIndex = IndexOfNode(siblings, previousSiblingId.Value);
            if (previousIndex >= 0)
            {
                insertIndex = previousIndex + 1;
            }
        }
        else if (nextSiblingId.HasValue)
        {
            int nextIndex = IndexOfNode(siblings, nextSiblingId.Value);
            if (nextIndex >= 0)
            {
                insertIndex = nextIndex;
            }
        }

        siblings.Insert(Math.Clamp(insertIndex, 0, siblings.Count), node);
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindLoadedSiblingCollection(long? parentId)
    {
        if (parentId is null)
        {
            return NoteRoots;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        return parent is { AreChildrenLoaded: true } ? parent.Children : null;
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(NoteTreeNodeViewModel target)
    {
        return FindOwningCollection(NoteRoots, target);
    }

    private static ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(
        ObservableCollection<NoteTreeNodeViewModel> nodes,
        NoteTreeNodeViewModel target)
    {
        if (nodes.Contains(target))
        {
            return nodes;
        }

        foreach (NoteTreeNodeViewModel node in nodes)
        {
            ObservableCollection<NoteTreeNodeViewModel>? result = FindOwningCollection(node.Children, target);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static void RemoveNodeById(ObservableCollection<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            NoteTreeNodeViewModel node = nodes[index];
            if (!node.IsPlaceholder && node.Id == id)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    private NoteTreeNodeViewModel? FindTreeNode(long id)
    {
        return FindTreeNode(NoteRoots, id);
    }

    private static NoteTreeNodeViewModel? FindTreeNode(IEnumerable<NoteTreeNodeViewModel> nodes, long id)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (!node.IsPlaceholder && node.Id == id)
            {
                return node;
            }

            NoteTreeNodeViewModel? result = FindTreeNode(node.Children, id);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void UpdateParentChildCount(long? parentId, int delta)
    {
        if (parentId is null)
        {
            return;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        if (parent is null)
        {
            return;
        }

        int childCount = parent.AreChildrenLoaded
            ? parent.Children.Count(child => !child.IsPlaceholder)
            : parent.ChildCount + delta;
        parent.UpdateChildCount(childCount);

        if (parent.AreChildrenLoaded || !parent.CanContainChildren)
        {
            return;
        }

        if (parent.ChildCount > 0 && parent.Children.Count == 0)
        {
            parent.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }
        else if (parent.ChildCount == 0)
        {
            parent.Children.Clear();
            parent.AreChildrenLoaded = true;
        }
    }

    private static int IndexOfNode(IReadOnlyList<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (!nodes[index].IsPlaceholder && nodes[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void RemovePlaceholders(ObservableCollection<NoteTreeNodeViewModel> nodes)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            if (nodes[index].IsPlaceholder)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    private HashSet<long> CaptureExpandedItemIds()
    {
        HashSet<long> expandedItemIds = [];
        CaptureExpandedItemIds(NoteRoots, expandedItemIds);
        return expandedItemIds;
    }

    private static void CaptureExpandedItemIds(
        IEnumerable<NoteTreeNodeViewModel> nodes,
        ISet<long> expandedItemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (node.IsPlaceholder)
            {
                continue;
            }

            if (node.IsExpanded)
            {
                expandedItemIds.Add(node.Id);
            }

            CaptureExpandedItemIds(node.Children, expandedItemIds);
        }
    }

    private void RestoreExpandedNodes(
        IEnumerable<NoteTreeNodeViewModel> nodes,
        IReadOnlySet<long> expandedItemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (node.IsPlaceholder || !expandedItemIds.Contains(node.Id))
            {
                continue;
            }

            LoadChildren(node);
            node.IsExpanded = true;
            RestoreExpandedNodes(node.Children, expandedItemIds);
        }
    }
}
