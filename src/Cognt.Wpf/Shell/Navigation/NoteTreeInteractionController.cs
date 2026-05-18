using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Navigation;

internal sealed partial class NoteTreeInteractionController
{
    private const string NoteTreeDragDataFormat = "CogntApp.NoteTree.ItemId";

    private readonly TreeView notesTree;
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<NoteTreeInteractionController> logger;
    private Point? dragStartPoint;
    private int selectionSuppressionDepth;

    internal NoteTreeInteractionController(
        TreeView notesTree,
        MainViewModel viewModel,
        IShellDialogService dialogs,
        ILogger<NoteTreeInteractionController> logger)
    {
        ArgumentNullException.ThrowIfNull(notesTree);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);

        this.notesTree = notesTree;
        this.viewModel = viewModel;
        this.dialogs = dialogs;
        this.logger = logger;
    }

    internal bool IsSelectionHandlingSuppressed => selectionSuppressionDepth > 0;

    internal void HandleSelectedItemChanged(object? selectedItem)
    {
        logger.LogDebug(
            "Note tree selection changed. SelectedType={SelectedType} IsLoadingCollection={IsLoadingCollection} Suppressed={Suppressed}",
            selectedItem?.GetType().Name,
            viewModel.IsLoadingCollection,
            IsSelectionHandlingSuppressed);
        if (viewModel.IsLoadingCollection || IsSelectionHandlingSuppressed)
        {
            return;
        }

        if (selectedItem is NoteTreeNodeViewModel node)
        {
            logger.LogInformation(
                "Handling note tree selection. NodeTitle={Title} NodeKind={Kind} NotePath={NotePath} IsPlaceholder={IsPlaceholder}",
                node.Title,
                node.Kind,
                node.NotePath,
                node.IsPlaceholder);
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

        if (FindTreeViewItem(e.OriginalSource) is TreeViewItem selectedItem
            && selectedItem.IsSelected
            && selectedItem.DataContext is NoteTreeNodeViewModel selectedNode
            && IsOpenableNode(selectedNode)
            && !viewModel.IsLoadingCollection
            && !IsSelectionHandlingSuppressed)
        {
            logger.LogDebug(
                "Retrying open for already-selected tree item. NodeTitle={Title} NodeKind={Kind} NotePath={NotePath}",
                selectedNode.Title,
                selectedNode.Kind,
                selectedNode.NotePath);
            notesTree.Dispatcher.BeginInvoke(
                () => HandleSelectedItemChanged(selectedNode),
                DispatcherPriority.Background);
        }

        dragStartPoint = e.GetPosition(notesTree);
    }

    private static bool IsOpenableNode(NoteTreeNodeViewModel node)
    {
        return !node.IsPlaceholder
            && (node.Kind == KnowledgeItemKind.Note
                || node.Kind == KnowledgeItemKind.PdfDocument);
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
        selectionSuppressionDepth++;

        try
        {
            action();
        }
        finally
        {
            if (deferRestore)
            {
                notesTree.Dispatcher.BeginInvoke(
                    ReleaseSelectionSuppression,
                    DispatcherPriority.Background);
            }
            else
            {
                ReleaseSelectionSuppression();
            }
        }
    }

    private void ReleaseSelectionSuppression()
    {
        if (selectionSuppressionDepth > 0)
        {
            selectionSuppressionDepth--;
        }
    }
}
