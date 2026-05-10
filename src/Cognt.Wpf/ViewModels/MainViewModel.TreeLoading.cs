using System.Collections.ObjectModel;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshNotesTree(bool preserveExpansion = false)
    {
        HashSet<long> expandedItemIds = preserveExpansion
            ? CollectExpandedItemIds(NoteRoots)
            : [];

        NoteRoots.Clear();

        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            RecentNotes.Clear();
            OnPropertyChanged(nameof(ShouldShowNoNotesState));
            return;
        }

        IsLoadingCollection = true;

        try
        {
            PopulateRecentNotes(LoadNoteCatalog(openCollectionPath));

            foreach (KnowledgeTreeItemReadModel item in noteService.LoadRootItems(openCollectionPath))
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
            IsLoadingCollection = false;
            OnPropertyChanged(nameof(ShouldShowNoNotesState));
        }
    }

    private IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(string collectionId)
    {
        return string.IsNullOrWhiteSpace(collectionId)
            ? []
            : noteService.LoadNoteCatalog(collectionId);
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

        node.Children.Clear();

        foreach (KnowledgeTreeItemReadModel child in noteService.LoadChildren(openCollectionPath, node.Id))
        {
            node.Children.Add(CreateTreeNode(child));
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
            item.ChildCount);

        if (node.CanContainChildren && item.ChildCount > 0)
        {
            node.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }

        return node;
    }

    private static HashSet<long> CollectExpandedItemIds(IEnumerable<NoteTreeNodeViewModel> nodes)
    {
        HashSet<long> itemIds = [];
        CollectExpandedItemIds(nodes, itemIds);
        return itemIds;
    }

    private static void CollectExpandedItemIds(IEnumerable<NoteTreeNodeViewModel> nodes, HashSet<long> itemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (!node.IsPlaceholder && node.IsExpanded)
            {
                itemIds.Add(node.Id);
            }

            CollectExpandedItemIds(node.Children, itemIds);
        }
    }

    private void RestoreExpandedNodes(
        ObservableCollection<NoteTreeNodeViewModel> nodes,
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
