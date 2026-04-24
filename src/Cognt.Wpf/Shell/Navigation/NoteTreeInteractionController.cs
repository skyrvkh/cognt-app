using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Navigation;

internal sealed partial class NoteTreeInteractionController
{
    private const string NoteTreeDragDataFormat = "CogntApp.NoteTree.ItemId";

    private readonly TreeView notesTree;
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private Point? dragStartPoint;
    private bool suppressSelectionHandling;

    internal NoteTreeInteractionController(
        TreeView notesTree,
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(notesTree);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.notesTree = notesTree;
        this.viewModel = viewModel;
        this.dialogs = dialogs;
    }

    internal bool IsSelectionHandlingSuppressed => suppressSelectionHandling;

    internal void HandleSelectedItemChanged(object? selectedItem)
    {
        if (viewModel.IsLoadingCollection || suppressSelectionHandling)
        {
            return;
        }

        NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.SelectTreeNode(selectedItem as NoteTreeNodeViewModel),
            dialogs,
            "Open Note Failed",
            "The selected note could not be opened: ");
    }

    internal void HandleExpanded(RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.OriginalSource is not TreeViewItem item
            || item.DataContext is not NoteTreeNodeViewModel node)
        {
            return;
        }

        NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.LoadChildren(node),
            dialogs,
            "Load Children Failed",
            "The child items could not be loaded: ");

        e.Handled = true;
    }

    internal static TreeViewItem? FindTreeViewItem(object? source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null)
        {
            if (current is TreeViewItem item)
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    internal void PreviewMouseLeftButtonDown(
        MouseButtonEventArgs e,
        Action<NoteTreeNodeViewModel> beginInlineRename)
    {
        if (e.ClickCount >= 2
            && FindTreeViewItem(e.OriginalSource) is TreeViewItem item
            && item.DataContext is NoteTreeNodeViewModel node)
        {
            e.Handled = true;
            RunWithSuppressedTreeSelection(() =>
            {
                item.IsSelected = true;
                item.Focus();
            });
            beginInlineRename(node);
            dragStartPoint = null;
            return;
        }

        dragStartPoint = e.GetPosition(notesTree);
    }

    internal void PreviewMouseMove(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || dragStartPoint is not Point startPoint)
        {
            dragStartPoint = null;
            return;
        }

        Point currentPoint = e.GetPosition(notesTree);
        if (Math.Abs(currentPoint.X - startPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPoint.Y - startPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem item
            || item.DataContext is not NoteTreeNodeViewModel node
            || !viewModel.CanMoveNode(node))
        {
            return;
        }

        dragStartPoint = null;
        DragDrop.DoDragDrop(item, new DataObject(NoteTreeDragDataFormat, node.Id), DragDropEffects.Move);
    }

    internal void RunWithSuppressedTreeSelection(Action action, bool deferRestore = false)
    {
        bool wasSuppressed = suppressSelectionHandling;
        suppressSelectionHandling = true;

        try
        {
            action();
        }
        finally
        {
            if (deferRestore)
            {
                notesTree.Dispatcher.BeginInvoke(
                    () => suppressSelectionHandling = wasSuppressed,
                    DispatcherPriority.Background);
            }
            else
            {
                suppressSelectionHandling = wasSuppressed;
            }
        }
    }
}
