using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CogntApp.ViewModels.Navigation;

namespace CogntApp;

public partial class MainWindow
{
    private void EditorBreadcrumbButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorBreadcrumbItemViewModel item)
        {
            return;
        }

        if (breadcrumbCommands.NavigateTo(item) && !item.IsNote)
        {
            SelectTreeItemById(item.Id);
        }
    }

    private void EditorBreadcrumbButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2
            || (sender as FrameworkElement)?.DataContext is not EditorBreadcrumbItemViewModel item)
        {
            return;
        }

        breadcrumbCommands.BeginRename(item);
        e.Handled = true;
    }

    private void EditorBreadcrumbButton_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorBreadcrumbItemViewModel item)
        {
            return;
        }

        breadcrumbCommands.BeginRename(item);
        e.Handled = true;
    }

    private void EditorBreadcrumbTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
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

    private void EditorBreadcrumbTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: EditorBreadcrumbItemViewModel item })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            breadcrumbCommands.CommitRename(item);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            item.CancelEdit();
            e.Handled = true;
        }
    }

    private void EditorBreadcrumbTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: EditorBreadcrumbItemViewModel item })
        {
            breadcrumbCommands.CommitRename(item);
            return;
        }

    }
}
