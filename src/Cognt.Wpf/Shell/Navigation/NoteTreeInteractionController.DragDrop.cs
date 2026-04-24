using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels;
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
        e.Effects = TryGetTreeDropTarget(e, out _, out _)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    internal void Drop(DragEventArgs e)
    {
        if (!TryGetTreeDropTarget(e, out long itemId, out TreeDropTarget target))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        NavigationShellOperationGuard.RunWithWarning(
            () => RunWithSuppressedTreeSelection(
                () => viewModel.MoveTreeNode(itemId, target.ParentId, target.PreviousSiblingId, target.NextSiblingId),
                deferRestore: true),
            dialogs,
            "Drag and Drop Failed",
            $"The item could not be moved:{Environment.NewLine}");
    }

    private bool TryGetTreeDropTarget(
        DragEventArgs e,
        out long itemId,
        out TreeDropTarget target)
    {
        itemId = 0;
        target = default;

        if (!e.Data.GetDataPresent(NoteTreeDragDataFormat)
            || e.Data.GetData(NoteTreeDragDataFormat) is not long draggedItemId
            || draggedItemId <= 0)
        {
            return false;
        }

        itemId = draggedItemId;

        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem targetItem)
        {
            target = new TreeDropTarget(ParentId: null, PreviousSiblingId: null, NextSiblingId: null);
            return !string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath);
        }

        if (targetItem.DataContext is not NoteTreeNodeViewModel targetNode
            || targetNode.IsPlaceholder
            || draggedItemId == targetNode.Id)
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
            ? GetPreviousSiblingId(targetNode, draggedItemId)
            : targetNode.Id;
        long? nextSiblingId = placement == TreeDropPlacement.Before
            ? targetNode.Id
            : GetNextSiblingId(targetNode, draggedItemId);

        target = new TreeDropTarget(parentId, previousSiblingId, nextSiblingId);
        return true;
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

    private long? GetPreviousSiblingId(NoteTreeNodeViewModel targetNode, long draggedItemId)
    {
        return GetAdjacentSiblingId(targetNode, draggedItemId, direction: -1);
    }

    private long? GetNextSiblingId(NoteTreeNodeViewModel targetNode, long draggedItemId)
    {
        return GetAdjacentSiblingId(targetNode, draggedItemId, direction: 1);
    }

    private long? GetAdjacentSiblingId(
        NoteTreeNodeViewModel targetNode,
        long draggedItemId,
        int direction)
    {
        IReadOnlyList<NoteTreeNodeViewModel>? siblings = FindSiblingList(viewModel.NoteRoots, targetNode);
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
                if (!candidate.IsPlaceholder && candidate.Id != draggedItemId)
                {
                    return candidate.Id;
                }
            }

            return null;
        }

        return null;
    }

    private static IReadOnlyList<NoteTreeNodeViewModel>? FindSiblingList(
        IReadOnlyList<NoteTreeNodeViewModel> nodes,
        NoteTreeNodeViewModel targetNode)
    {
        if (nodes.Contains(targetNode))
        {
            return nodes;
        }

        foreach (NoteTreeNodeViewModel node in nodes)
        {
            IReadOnlyList<NoteTreeNodeViewModel>? childResult = FindSiblingList(node.Children, targetNode);
            if (childResult is not null)
            {
                return childResult;
            }
        }

        return null;
    }
}
