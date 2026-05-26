using System.Collections.ObjectModel;
using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshNotesTree(bool preserveExpansion = false)
    {
        HashSet<long> expandedItemIds = preserveExpansion
            ? CaptureExpandedItemIds()
            : [];

        IsLoadingCollection = true;

        try
        {
            NoteRoots.Clear();
            ResetLoadedTreeNodeIndex();

            if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
            {
                return;
            }

            IReadOnlyList<KnowledgeNoteListItemReadModel> noteCatalog = LoadNoteCatalog(openCollectionPath);
            PopulateRecentNotes(noteCatalog);

            foreach (KnowledgeTreeItemReadModel item in noteService.LoadRootItems(openCollectionPath))
            {
                NoteTreeNodeViewModel node = CreateTreeNode(item);
                NoteRoots.Add(node);
                IndexLoadedTreeSubtree(node);
            }

            if (expandedItemIds.Count > 0)
            {
                RestoreExpandedNodes(NoteRoots, expandedItemIds);
            }
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            NoteRoots.Clear();
            ResetLoadedTreeNodeIndex();
            RecentNotes.Clear();
            RaiseMessage("Collection Load Failed", $"The collection could not be read: {ex.Message}");
        }
        finally
        {
            IsLoadingCollection = false;
            OnPropertyChanged(nameof(ShouldShowNoNotesState));
        }
    }

    private IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(string collectionPath)
    {
        return noteService.LoadNoteCatalog(collectionPath);
    }

    public void LoadChildren(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath)
            || node.IsPlaceholder
            || !node.CanContainChildren
            || node.AreChildrenLoaded)
        {
            return;
        }

        foreach (NoteTreeNodeViewModel child in node.Children)
        {
            UnindexLoadedTreeSubtree(child);
        }

        node.Children.Clear();

        foreach (KnowledgeTreeItemReadModel child in noteService.LoadChildren(openCollectionPath, node.Id))
        {
            NoteTreeNodeViewModel childNode = CreateTreeNode(child);
            node.Children.Add(childNode);
            IndexLoadedTreeSubtree(childNode);
        }

        node.MarkChildrenLoaded();
    }

    private NoteTreeNodeViewModel CreateTreeNode(KnowledgeTreeItemReadModel item)
    {
        string? notePath = ResolveNotePath(item);

        NoteTreeNodeViewModel node = new(
            item.Id,
            item.ParentId,
            item.Kind,
            item.Title,
            item.FileRelPath,
            notePath,
            item.ChildCount)
        {
            IsExpanded = false,
        };

        if (item.ChildCount > 0 && node.CanContainChildren)
        {
            node.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }

        return node;
    }

    private HashSet<long> CaptureExpandedItemIds()
    {
        HashSet<long>? expandedItemIds = null;
        foreach (NoteTreeNodeViewModel node in loadedTreeNodesById.Values)
        {
            if (!node.IsExpanded)
            {
                continue;
            }

            expandedItemIds ??= new HashSet<long>(Math.Min(loadedTreeNodesById.Count, 64));
            expandedItemIds.Add(node.Id);
        }

        return expandedItemIds ?? [];
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
