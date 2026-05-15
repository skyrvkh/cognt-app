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

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        noteTreeInteractions.HandleSelectedItemChanged(e.NewValue);
    }

    private void NotesTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        noteTreeInteractions.HandleExpanded(e);
    }

    private void NotesTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            RenameSelectedTreeNode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteSelectedTreeNode();
            e.Handled = true;
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

    private void NewFolderTreeItem_Click(object sender, RoutedEventArgs e)
    {
        noteTreeCommands.CreateFolder(noteTreeContextNode, BeginInlineRenameById);
    }

    private void DeleteTreeItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteTreeNode(noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel));
    }

    private void DuplicateTreeItem_Click(object sender, RoutedEventArgs e)
    {
        noteTreeCommands.DuplicateNode(noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel));
    }

    private void NotesTreeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        NoteTreeNodeViewModel? node = noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel);
        DuplicateTreeMenuItem.IsEnabled = viewModel.CanDuplicateNode(node);
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
        noteTreeCommands.BeginInlineRename(node);
    }

    private void DeleteSelectedTreeNode()
    {
        noteTreeCommands.DeleteNode(NotesTree.SelectedItem as NoteTreeNodeViewModel);
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

    private void DeleteTreeNode(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.DeleteNode(node);
    }

    private void NotesTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        noteTreeInteractions.PreviewMouseLeftButtonDown(e, noteTreeCommands.BeginInlineRename);
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
