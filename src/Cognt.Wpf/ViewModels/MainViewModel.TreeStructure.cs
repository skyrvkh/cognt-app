using System.Collections.ObjectModel;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void AddLoadedTreeNode(KnowledgeTreeItemReadModel? item)
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
        OnPropertyChanged(nameof(ShouldShowNoNotesState));
    }

    private void RemoveLoadedTreeNode(NoteTreeNodeViewModel node)
    {
        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindOwningCollection(node);

        if (siblings is not null)
        {
            siblings.Remove(node);
        }

        UpdateParentChildCount(node.ParentId, delta: -1);
        OnPropertyChanged(nameof(ShouldShowNoNotesState));
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
}
