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

            if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
            {
                return;
            }

            IReadOnlyList<KnowledgeNoteListItemReadModel> noteCatalog = LoadNoteCatalog(openCollectionPath);
            PopulateRecentNotes(noteCatalog);

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
