using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CogntApp.Shell.Navigation;
using CogntApp.ViewModels;

namespace CogntApp;

public partial class MainWindow
{
    private NoteTreeNodeViewModel? noteTreeContextNode;
    private NoteTreeNodeViewModel? selectedNoteTreeNode;

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        UpdateTrackedSelectedTreeNode(e.NewValue as NoteTreeNodeViewModel);
        noteTreeInteractions.HandleSelectedItemChanged(e.NewValue);
    }

    private void NotesTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        noteTreeInteractions.HandleExpanded(e);
    }

    private async void NotesTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            noteTreeInteractions.OpenSelectedItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            noteTreeInteractions.ClearTreeSelection();
            UpdateTrackedSelectedTreeNode(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            RenameSelectedTreeNode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteSelectedTreeNodeAsync();
        }
    }

    private void NotesTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NoteTreeInteractionController.FindTreeViewItem(e.OriginalSource) is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
            noteTreeContextNode = item.DataContext as NoteTreeNodeViewModel;
            return;
        }

        noteTreeContextNode = null;
    }

    private void NewNoteTreeItem_Click(object sender, RoutedEventArgs e)
    {
        noteTreeCommands.CreateNote(noteTreeContextNode, BeginInlineRenameById);
    }

    private void NewFolderTreeItem_Click(object sender, RoutedEventArgs e)
    {
        noteTreeCommands.CreateFolder(noteTreeContextNode, BeginInlineRenameById);
    }

    private async void DeleteTreeItem_Click(object sender, RoutedEventArgs e)
    {
        NoteTreeNodeViewModel? targetNode = noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel);
        await DeleteTreeNodeAsync(targetNode);
    }

    private void DuplicateTreeItem_Click(object sender, RoutedEventArgs e)
    {
        noteTreeCommands.DuplicateNode(noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel));
    }

    private void NotesTreeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        NoteTreeNodeViewModel? node = noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel);
        NewNoteTreeMenuItem.IsEnabled = viewModel.CanCreateNoteInNode(node);
        NewFolderTreeMenuItem.IsEnabled = viewModel.CanCreateFolderInNode(node);
        DuplicateTreeMenuItem.IsEnabled = viewModel.CanDuplicateNode(node);
        bool canEditTags = node is { Id: > 0, IsPlaceholder: false };
        EditTagsTreeMenuItem.IsEnabled = canEditTags;
    }

    private void RenameSelectedTreeNode()
    {
        noteTreeCommands.BeginInlineRename(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void NotesTree_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NoteTreeInteractionController.FindTreeViewItem(e.OriginalSource) is not TreeViewItem item
            || item.DataContext is not NoteTreeNodeViewModel node)
        {
            return;
        }

        item.IsSelected = true;
        item.Focus();
        e.Handled = true;
        if (node.IsFlashcardNode || !string.IsNullOrWhiteSpace(node.NotePath))
        {
            noteTreeInteractions.OpenNode(node);
            return;
        }

        noteTreeCommands.BeginInlineRename(node);
    }

    private Task DeleteSelectedTreeNodeAsync()
    {
        return DeleteTreeNodeAsync(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void CommitInlineRename(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.CommitInlineRename(node, static item => item.BeginRename());
    }

    private void TreeRenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || e.NewValue is not true)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                textBox.Focus();
                textBox.SelectAll();
            },
            DispatcherPriority.Input);
    }

    private void TreeRenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || textBox.DataContext is not NoteTreeNodeViewModel node)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitInlineRename(node);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            node.CancelRename();
            e.Handled = true;
        }
    }

    private void TreeRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: NoteTreeNodeViewModel node })
        {
            CommitInlineRename(node);
        }
    }

    private async Task DeleteTreeNodeAsync(NoteTreeNodeViewModel? node)
    {
        IReadOnlyList<NoteTreeNodeViewModel> nodes = noteTreeInteractions.GetSelectedCommandNodes(node);
        bool deleted = await noteTreeCommands.DeleteNodesAsync(nodes);
        if (!deleted)
        {
            return;
        }

        noteTreeContextNode = null;
        noteTreeInteractions.ClearTreeSelection();
        UpdateTrackedSelectedTreeNode(null);
    }

    private static string FormatNoteTreeNodeForLog(NoteTreeNodeViewModel? node)
    {
        return node is null
            ? "(null)"
            : $"Id={node.Id}, Title={node.Title}, Kind={node.Kind}, IsMultiSelected={node.IsMultiSelected}, ParentId={node.ParentId}, FileRelPath={node.FileRelPath}";
    }

    private void NotesTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        noteTreeInteractions.PreviewMouseLeftButtonDown(e, noteTreeCommands.BeginInlineRename);
    }

    private void NotesTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        noteTreeInteractions.PreviewMouseLeftButtonUp(e);
    }

    private void NotesTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        noteTreeInteractions.PreviewMouseMove(e);
    }

    private void NotesTree_DragOver(object sender, DragEventArgs e)
    {
        noteTreeInteractions.DragOver(e);
    }

    private void NotesTree_Drop(object sender, DragEventArgs e)
    {
        noteTreeInteractions.Drop(e);
    }

}
