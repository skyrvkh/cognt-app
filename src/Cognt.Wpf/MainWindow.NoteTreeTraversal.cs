using System.Windows.Controls;
using System.Windows.Threading;
using CogntApp.ViewModels;

namespace CogntApp;

public partial class MainWindow
{
    private void BeginInlineRenameById(string itemId)
    {
        if (!long.TryParse(itemId, out long id))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                if (!viewModel.TryGetLoadedTreePathById(id, out IReadOnlyList<NoteTreeNodeViewModel>? path)
                    || path is null)
                {
                    return;
                }

                SelectTreeNodePath(path);
                noteTreeCommands.BeginInlineRename(path[^1]);
            },
            DispatcherPriority.Background);
    }

    private void SelectNote(string notePath)
    {
        if (viewModel.TryGetLoadedTreePathByNotePath(notePath, out IReadOnlyList<NoteTreeNodeViewModel>? path)
            && path is not null)
        {
            SelectTreeNodePath(path);
        }
    }

    private void SelectTreeItemById(long itemId)
    {
        if (viewModel.TryGetLoadedTreePathById(itemId, out IReadOnlyList<NoteTreeNodeViewModel>? path)
            && path is not null)
        {
            SelectTreeNodePath(path);
        }
    }

    private void SelectTreeNodePath(IReadOnlyList<NoteTreeNodeViewModel> path)
    {
        if (path.Count == 0)
        {
            return;
        }

        for (int index = 0; index < path.Count - 1; index++)
        {
            path[index].IsExpanded = true;
        }

        UpdateTrackedSelectedTreeNode(path[^1]);
        TryFocusTreeNodePath(path);
    }

    private void UpdateTrackedSelectedTreeNode(NoteTreeNodeViewModel? node)
    {
        if (ReferenceEquals(selectedNoteTreeNode, node))
        {
            if (node is not null && !node.IsSelected)
            {
                node.IsSelected = true;
            }

            return;
        }

        if (selectedNoteTreeNode is not null && selectedNoteTreeNode.IsSelected)
        {
            selectedNoteTreeNode.IsSelected = false;
        }

        selectedNoteTreeNode = node;

        if (selectedNoteTreeNode is not null && !selectedNoteTreeNode.IsSelected)
        {
            selectedNoteTreeNode.IsSelected = true;
        }
    }

    private void TryFocusTreeNodePath(IReadOnlyList<NoteTreeNodeViewModel> path)
    {
        NotesTree.UpdateLayout();

        if (TryResolveTreeItem(NotesTree, path, 0) is not TreeViewItem item)
        {
            return;
        }

        item.BringIntoView();
        item.Focus();
    }

    private static TreeViewItem? TryResolveTreeItem(
        ItemsControl parent,
        IReadOnlyList<NoteTreeNodeViewModel> path,
        int depth)
    {
        if (depth >= path.Count
            || parent.ItemContainerGenerator.ContainerFromItem(path[depth]) is not TreeViewItem item)
        {
            return null;
        }

        if (depth == path.Count - 1)
        {
            return item;
        }

        item.IsExpanded = true;
        item.UpdateLayout();
        return TryResolveTreeItem(item, path, depth + 1);
    }
}
