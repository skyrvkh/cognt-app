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
    private const string NoteTreeDragItemIdsDataFormat = "CogntApp.NoteTree.ItemIds";

    private readonly TreeView notesTree;
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<NoteTreeInteractionController> logger;
    private readonly HashSet<long> selectedNodeIds = [];
    private Point? dragStartPoint;
    private TreeViewItem? dragStartItem;
    private NoteTreeNodeViewModel? dragStartNode;
    private NoteTreeNodeViewModel? pendingClickOpenNode;
    private NoteTreeNodeViewModel? selectionAnchorNode;
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
        if (viewModel.IsLoadingCollection || IsSelectionHandlingSuppressed)
        {
            return;
        }

        if (selectedItem is NoteTreeNodeViewModel node)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed || dragStartNode is not null)
            {
                pendingClickOpenNode = IsPlainSingleSelectionGesture() ? node : null;
            }
        }
    }

    internal void OpenSelectedItem()
    {
        OpenNode(notesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    internal void OpenNode(NoteTreeNodeViewModel? node)
    {
        if (node is null || node.IsPlaceholder || !IsOpenableNode(node))
        {
            return;
        }

        string title = node.IsFlashcardNode ? "Open Card Failed" : "Open Note Failed";
        string message = node.IsFlashcardNode
            ? "The selected flashcard could not be opened: "
            : "The selected note could not be opened: ";
        NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.SelectTreeNode(node),
            dialogs,
            title,
            message);
    }

    internal void ClearTreeSelection()
    {
        ClearMultiSelection();
        ClearDragStart();
        ClearPendingClickOpen();
        selectionAnchorNode = null;

        if (notesTree.SelectedItem is not NoteTreeNodeViewModel selectedNode)
        {
            return;
        }

        RunWithSuppressedTreeSelection(() => selectedNode.IsSelected = false);
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
        TreeViewItem? item = FindTreeViewItem(e.OriginalSource);
        NoteTreeNodeViewModel? node = item?.DataContext as NoteTreeNodeViewModel;

        if (e.ClickCount >= 2
            && item is not null
            && node is not null)
        {
            e.Handled = true;
            ClearPendingClickOpen();
            ClearMultiSelection();
            RunWithSuppressedTreeSelection(() =>
            {
                item.IsSelected = true;
                item.Focus();
            });
            if (IsOpenableNode(node))
            {
                OpenNode(node);
                ClearDragStart();
                return;
            }

            beginInlineRename(node);
            ClearDragStart();
            return;
        }

        if (item is null || node is null)
        {
            ClearTreeSelection();
            return;
        }

        dragStartPoint = e.GetPosition(notesTree);
        dragStartItem = item;
        dragStartNode = node;

        if (TryHandleMultiSelectionGesture(item, node, e))
        {
            return;
        }

        if (!node.IsMultiSelected || selectedNodeIds.Count <= 1)
        {
            ClearMultiSelection();
        }

        selectionAnchorNode = node;
    }

    internal void PreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        TreeViewItem? item = FindTreeViewItem(e.OriginalSource);
        NoteTreeNodeViewModel? node = item?.DataContext as NoteTreeNodeViewModel;
        NoteTreeNodeViewModel? pendingNode = dragStartNode;
        TreeViewItem? pendingItem = dragStartItem;
        NoteTreeNodeViewModel? pendingOpenNode = pendingClickOpenNode;
        ClearDragStart();
        ClearPendingClickOpen();

        bool isSameMouseDownItem = item is not null
            && node is not null
            && pendingItem is not null
            && pendingNode is not null
            && ReferenceEquals(item, pendingItem)
            && IsSameNode(node, pendingNode);
        bool isPendingSelectedItem = item is not null
            && node is not null
            && pendingOpenNode is not null
            && IsSameNode(node, pendingOpenNode);

        if (!isSameMouseDownItem && !isPendingSelectedItem)
        {
            return;
        }

        if (!IsPlainSingleSelectionGesture())
        {
            return;
        }

        NoteTreeNodeViewModel nodeToOpen = isPendingSelectedItem
            ? pendingOpenNode!
            : node!;
        OpenNode(nodeToOpen);
    }

    internal void PreviewMouseMove(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || dragStartPoint is not Point startPoint)
        {
            ClearDragStart();
            return;
        }

        Point currentPoint = e.GetPosition(notesTree);
        if (Math.Abs(currentPoint.X - startPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPoint.Y - startPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (dragStartNode is not NoteTreeNodeViewModel node
            || !viewModel.CanMoveNode(node))
        {
            return;
        }

        long[] draggedItemIds = CreateDragItemIds(node);
        if (draggedItemIds.Length == 0)
        {
            return;
        }

        DependencyObject dragSource = dragStartItem is not null ? dragStartItem : notesTree;
        ClearDragStart();
        ClearPendingClickOpen();
        DragDrop.DoDragDrop(dragSource, CreateDragDataObject(draggedItemIds), DragDropEffects.Move);
    }

    private bool IsPlainSingleSelectionGesture()
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        return !viewModel.IsLoadingCollection
            && !IsSelectionHandlingSuppressed
            && (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None
            && selectedNodeIds.Count <= 1;
    }

    private static bool IsSameNode(NoteTreeNodeViewModel left, NoteTreeNodeViewModel right)
    {
        return ReferenceEquals(left, right) || left.Id == right.Id;
    }

    private static bool IsOpenableNode(NoteTreeNodeViewModel node)
    {
        return node.IsFlashcardNode
            || node.Kind == KnowledgeItemKind.PdfDocument
            || !string.IsNullOrWhiteSpace(node.NotePath);
    }

    private void ClearPendingClickOpen()
    {
        pendingClickOpenNode = null;
    }

    private static DataObject CreateDragDataObject(IReadOnlyList<long> draggedItemIds)
    {
        DataObject data = new();
        data.SetData(NoteTreeDragDataFormat, draggedItemIds[0]);
        data.SetData(NoteTreeDragItemIdsDataFormat, draggedItemIds.ToArray());
        return data;
    }

    private void ClearDragStart()
    {
        dragStartPoint = null;
        dragStartItem = null;
        dragStartNode = null;
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
