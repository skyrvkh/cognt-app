using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using NoteTreeNodeViewModel = CogntApp.ViewModels.NoteTreeNodeViewModel;

namespace CogntApp.Shell.Navigation;

internal sealed partial class NoteTreeInteractionController
{
    private enum TreeDropPlacement
    {
        Before,
        After,
        Inside,
    }

    private readonly struct TreeDropTarget
    {
        internal TreeDropTarget(long? ParentId, long? PreviousSiblingId, long? NextSiblingId)
        {
            this.ParentId = ParentId;
            this.PreviousSiblingId = PreviousSiblingId;
            this.NextSiblingId = NextSiblingId;
        }

        internal long? ParentId { get; }

        internal long? PreviousSiblingId { get; }

        internal long? NextSiblingId { get; }
    }

    internal void DragOver(DragEventArgs e)
    {
        bool hasTarget = TryGetTreeDropTarget(e, out IReadOnlyList<long> itemIds, out TreeDropTarget target);
        e.Effects = hasTarget ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    internal void Drop(DragEventArgs e)
    {
        if (!TryGetTreeDropTarget(e, out IReadOnlyList<long> itemIds, out TreeDropTarget target))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        NavigationShellOperationGuard.RunWithWarning(
            () => RunWithSuppressedTreeSelection(
                () => viewModel.MoveTreeNodes(itemIds, target.ParentId, target.PreviousSiblingId, target.NextSiblingId),
                deferRestore: true),
            dialogs,
            "Drag and Drop Failed",
            $"The item could not be moved:{Environment.NewLine}",
            logger);
    }

    private bool TryGetTreeDropTarget(
        DragEventArgs e,
        out IReadOnlyList<long> itemIds,
        out TreeDropTarget target)
    {
        itemIds = [];
        target = default;

        if (!TryGetDraggedItemIds(e.Data, out long[] draggedItemIds))
        {
            return false;
        }

        itemIds = draggedItemIds;
        HashSet<long> draggedItemIdSet = [.. draggedItemIds];

        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem targetItem)
        {
            target = new TreeDropTarget(ParentId: null, PreviousSiblingId: null, NextSiblingId: null);
            bool canDropAtRoot = !string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath);
            return canDropAtRoot;
        }

        if (targetItem.DataContext is not NoteTreeNodeViewModel targetNode
            || targetNode.IsPlaceholder
            || targetNode.IsFlashcardNode
            || draggedItemIdSet.Contains(targetNode.Id)
            || IsDropTargetInsideDraggedSubtree(targetNode, draggedItemIdSet))
        {
            return false;
        }

        TreeDropPlacement placement = GetDropPlacement(targetItem, targetNode, e);

        if (placement == TreeDropPlacement.Inside)
        {
            target = new TreeDropTarget(targetNode.Id, PreviousSiblingId: null, NextSiblingId: null);
            return true;
        }

        long? parentId = targetNode.ParentId;
        long? previousSiblingId = placement == TreeDropPlacement.Before
            ? GetPreviousSiblingId(targetNode, draggedItemIdSet)
            : targetNode.Id;
        long? nextSiblingId = placement == TreeDropPlacement.Before
            ? targetNode.Id
            : GetNextSiblingId(targetNode, draggedItemIdSet);

        target = new TreeDropTarget(parentId, previousSiblingId, nextSiblingId);
        return true;
    }

    private static bool TryGetDraggedItemIds(IDataObject data, out long[] itemIds)
    {
        itemIds = [];

        if (data.GetDataPresent(NoteTreeDragItemIdsDataFormat)
            && data.GetData(NoteTreeDragItemIdsDataFormat) is long[] draggedItemIds)
        {
            itemIds = [.. draggedItemIds.Where(static id => id > 0).Distinct()];
            return itemIds.Length > 0;
        }

        if (data.GetDataPresent(NoteTreeDragDataFormat)
            && data.GetData(NoteTreeDragDataFormat) is long draggedItemId
            && draggedItemId > 0)
        {
            itemIds = [draggedItemId];
            return true;
        }

        return false;
    }

    private bool IsDropTargetInsideDraggedSubtree(
        NoteTreeNodeViewModel targetNode,
        IReadOnlySet<long> draggedItemIds)
    {
        if (!viewModel.TryGetLoadedTreePathById(targetNode.Id, out IReadOnlyList<NoteTreeNodeViewModel>? path)
            || path is null)
        {
            return false;
        }

        foreach (NoteTreeNodeViewModel pathNode in path)
        {
            if (!pathNode.IsPlaceholder && draggedItemIds.Contains(pathNode.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static TreeDropPlacement GetDropPlacement(
        TreeViewItem item,
        NoteTreeNodeViewModel targetNode,
        DragEventArgs e)
    {
        double height = Math.Max(1, item.ActualHeight);
        double y = e.GetPosition(item).Y;

        if (targetNode.CanContainChildren && y > height * 0.25 && y < height * 0.75)
        {
            return TreeDropPlacement.Inside;
        }

        return y < height / 2
            ? TreeDropPlacement.Before
            : TreeDropPlacement.After;
    }

    private long? GetPreviousSiblingId(NoteTreeNodeViewModel targetNode, IReadOnlySet<long> draggedItemIds)
    {
        return GetAdjacentSiblingId(targetNode, draggedItemIds, direction: -1);
    }

    private long? GetNextSiblingId(NoteTreeNodeViewModel targetNode, IReadOnlySet<long> draggedItemIds)
    {
        return GetAdjacentSiblingId(targetNode, draggedItemIds, direction: 1);
    }

    private long? GetAdjacentSiblingId(
        NoteTreeNodeViewModel targetNode,
        IReadOnlySet<long> draggedItemIds,
        int direction)
    {
        IReadOnlyList<NoteTreeNodeViewModel>? siblings = viewModel.GetLoadedSiblingList(targetNode);
        if (siblings is null)
        {
            return null;
        }

        for (int index = 0; index < siblings.Count; index++)
        {
            if (!ReferenceEquals(siblings[index], targetNode))
            {
                continue;
            }

            for (int siblingIndex = index + direction;
                 siblingIndex >= 0 && siblingIndex < siblings.Count;
                 siblingIndex += direction)
            {
                NoteTreeNodeViewModel candidate = siblings[siblingIndex];
                if (!candidate.IsPlaceholder && !draggedItemIds.Contains(candidate.Id))
                {
                    return candidate.Id;
                }
            }

            return null;
        }

        return null;
    }
}
