using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Navigation;

internal sealed partial class NoteTreeInteractionController
{
    private bool TryHandleMultiSelectionGesture(
        TreeViewItem item,
        NoteTreeNodeViewModel node,
        MouseButtonEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool isControlSelection = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool isShiftSelection = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if ((!isControlSelection && !isShiftSelection) || !viewModel.CanMoveNode(node))
        {
            return false;
        }

        ClearPendingClickOpen();
        e.Handled = true;
        item.Focus();

        if (isShiftSelection)
        {
            SelectRange(node);
            return true;
        }

        if (selectedNodeIds.Count == 0
            && notesTree.SelectedItem is NoteTreeNodeViewModel activeNode
            && !ReferenceEquals(activeNode, node)
            && viewModel.CanMoveNode(activeNode))
        {
            SetMultiSelected(activeNode, isSelected: true);
            selectionAnchorNode = activeNode;
        }

        ToggleMultiSelected(node);
        selectionAnchorNode ??= node;
        return true;
    }

    private void SelectRange(NoteTreeNodeViewModel node)
    {
        NoteTreeNodeViewModel anchor = ResolveSelectionAnchor(node);
        IReadOnlyList<NoteTreeNodeViewModel> visibleNodes = GetVisibleMovableNodes();
        int anchorIndex = IndexOfNode(visibleNodes, anchor);
        int nodeIndex = IndexOfNode(visibleNodes, node);

        if (anchorIndex < 0 || nodeIndex < 0)
        {
            ClearMultiSelection(clearAnchor: false);
            SetMultiSelected(node, isSelected: true);
            selectionAnchorNode = node;
            return;
        }

        int startIndex = Math.Min(anchorIndex, nodeIndex);
        int endIndex = Math.Max(anchorIndex, nodeIndex);

        ClearMultiSelection(clearAnchor: false);
        for (int index = startIndex; index <= endIndex; index++)
        {
            SetMultiSelected(visibleNodes[index], isSelected: true);
        }

        selectionAnchorNode = anchor;
    }

    private NoteTreeNodeViewModel ResolveSelectionAnchor(NoteTreeNodeViewModel fallback)
    {
        if (selectionAnchorNode is { } anchor
            && viewModel.CanMoveNode(anchor))
        {
            return anchor;
        }

        return notesTree.SelectedItem is NoteTreeNodeViewModel activeNode
            && viewModel.CanMoveNode(activeNode)
                ? activeNode
                : fallback;
    }

    private void ToggleMultiSelected(NoteTreeNodeViewModel node)
    {
        SetMultiSelected(node, !node.IsMultiSelected);
        if (selectedNodeIds.Count == 0)
        {
            selectionAnchorNode = null;
        }
    }

    private void SetMultiSelected(NoteTreeNodeViewModel node, bool isSelected)
    {
        if (node.IsPlaceholder || !viewModel.CanMoveNode(node))
        {
            return;
        }

        node.IsMultiSelected = isSelected;
        if (isSelected)
        {
            selectedNodeIds.Add(node.Id);
        }
        else
        {
            selectedNodeIds.Remove(node.Id);
        }
    }

    private void ClearMultiSelection(bool clearAnchor = true)
    {
        if (selectedNodeIds.Count > 0)
        {
            foreach (NoteTreeNodeViewModel node in EnumerateLoadedTreeNodes())
            {
                if (node.IsMultiSelected)
                {
                    node.IsMultiSelected = false;
                }
            }

            selectedNodeIds.Clear();
        }

        if (clearAnchor)
        {
            selectionAnchorNode = null;
        }
    }

    internal IReadOnlyList<NoteTreeNodeViewModel> GetSelectedCommandNodes(NoteTreeNodeViewModel? fallback)
    {
        if (fallback is null)
        {
            return [];
        }

        bool fallbackIsInMultiSelection = fallback.IsMultiSelected || selectedNodeIds.Contains(fallback.Id);
        if (!fallbackIsInMultiSelection || selectedNodeIds.Count <= 1)
        {
            bool canDeleteFallback = viewModel.CanDeleteNode(fallback);
            return canDeleteFallback ? [fallback] : [];
        }

        List<NoteTreeNodeViewModel> selectedNodes = [];
        HashSet<long> selectedIds = [];
        foreach (NoteTreeNodeViewModel node in EnumerateLoadedTreeNodes())
        {
            if ((node.IsMultiSelected || selectedNodeIds.Contains(node.Id)) && viewModel.CanDeleteNode(node))
            {
                selectedNodes.Add(node);
                selectedIds.Add(node.Id);
            }
        }

        if (!selectedIds.Contains(fallback.Id))
        {
            bool canDeleteFallback = viewModel.CanDeleteNode(fallback);
            return canDeleteFallback ? [fallback] : [];
        }

        List<NoteTreeNodeViewModel> rootNodes = [];
        foreach (NoteTreeNodeViewModel node in selectedNodes)
        {
            if (!HasSelectedAncestor(node, selectedIds))
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes.Count > 0 ? [.. rootNodes] : [fallback];
    }

    private long[] CreateDragItemIds(NoteTreeNodeViewModel node)
    {
        if (!node.IsMultiSelected || selectedNodeIds.Count <= 1)
        {
            return [node.Id];
        }

        HashSet<long> selectedIds = [];
        List<NoteTreeNodeViewModel> selectedNodes = [];
        foreach (NoteTreeNodeViewModel loadedNode in EnumerateLoadedTreeNodes())
        {
            if (loadedNode.IsMultiSelected && viewModel.CanMoveNode(loadedNode))
            {
                selectedIds.Add(loadedNode.Id);
                selectedNodes.Add(loadedNode);
            }
        }

        if (!selectedIds.Contains(node.Id))
        {
            return [node.Id];
        }

        List<long> rootItemIds = [];
        foreach (NoteTreeNodeViewModel selectedNode in selectedNodes)
        {
            if (!HasSelectedAncestor(selectedNode, selectedIds))
            {
                rootItemIds.Add(selectedNode.Id);
            }
        }

        return rootItemIds.Count > 0 ? [.. rootItemIds] : [node.Id];
    }

    private bool HasSelectedAncestor(NoteTreeNodeViewModel node, IReadOnlySet<long> selectedIds)
    {
        if (!viewModel.TryGetLoadedTreePathById(node.Id, out IReadOnlyList<NoteTreeNodeViewModel>? path)
            || path is null)
        {
            return false;
        }

        for (int index = 0; index < path.Count - 1; index++)
        {
            NoteTreeNodeViewModel ancestor = path[index];
            if (!ancestor.IsPlaceholder && selectedIds.Contains(ancestor.Id))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<NoteTreeNodeViewModel> GetVisibleMovableNodes()
    {
        List<NoteTreeNodeViewModel> nodes = [];
        AppendVisibleNodes(viewModel.NoteRoots, nodes);
        return nodes;
    }

    private void AppendVisibleNodes(
        IEnumerable<NoteTreeNodeViewModel> source,
        ICollection<NoteTreeNodeViewModel> destination)
    {
        foreach (NoteTreeNodeViewModel node in source)
        {
            if (!node.IsPlaceholder && viewModel.CanMoveNode(node))
            {
                destination.Add(node);
            }

            if (!node.IsPlaceholder && node.IsExpanded && node.AreChildrenLoaded)
            {
                AppendVisibleNodes(node.Children, destination);
            }
        }
    }

    private IEnumerable<NoteTreeNodeViewModel> EnumerateLoadedTreeNodes()
    {
        foreach (NoteTreeNodeViewModel node in viewModel.NoteRoots)
        {
            foreach (NoteTreeNodeViewModel descendant in EnumerateLoadedTreeNodes(node))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<NoteTreeNodeViewModel> EnumerateLoadedTreeNodes(NoteTreeNodeViewModel node)
    {
        yield return node;

        foreach (NoteTreeNodeViewModel child in node.Children)
        {
            foreach (NoteTreeNodeViewModel descendant in EnumerateLoadedTreeNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static int IndexOfNode(IReadOnlyList<NoteTreeNodeViewModel> nodes, NoteTreeNodeViewModel target)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], target) || nodes[index].Id == target.Id)
            {
                return index;
            }
        }

        return -1;
    }
}
