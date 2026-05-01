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
                foreach (TreeViewItem item in EnumerateTreeItems(NotesTree))
                {
                    if (item.DataContext is NoteTreeNodeViewModel node && node.Id == id)
                    {
                        item.IsSelected = true;
                        item.Focus();
                        noteTreeCommands.BeginInlineRename(node);
                        return;
                    }
                }
            },
            DispatcherPriority.Background);
    }

    private void SelectNote(string notePath)
    {
        foreach (TreeViewItem item in EnumerateTreeItems(NotesTree))
        {
            if (item.DataContext is NoteTreeNodeViewModel { NotePath: string itemNotePath }
                && string.Equals(itemNotePath, notePath, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
                item.Focus();
                return;
            }
        }
    }

    private void SelectTreeItemById(long itemId)
    {
        foreach (TreeViewItem item in EnumerateTreeItems(NotesTree))
        {
            if (item.DataContext is NoteTreeNodeViewModel node && node.Id == itemId)
            {
                item.IsSelected = true;
                item.Focus();
                return;
            }
        }
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(ItemsControl parent)
    {
        parent.UpdateLayout();

        foreach (object dataItem in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(dataItem) is not TreeViewItem item)
            {
                continue;
            }

            yield return item;

            foreach (TreeViewItem child in EnumerateTreeItems(item))
            {
                yield return child;
            }
        }
    }
}
