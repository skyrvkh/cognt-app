using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels;

namespace CogntApp.Views;

public partial class AllNotesView : UserControl
{
    public AllNotesView()
    {
        InitializeComponent();
    }

    private void NotesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AllNotesViewModel viewModel
            && sender is DataGrid { SelectedItem: AllNotesListItemViewModel item }
            && viewModel.OpenNoteCommand.CanExecute(item))
        {
            viewModel.OpenNoteCommand.Execute(item);
        }
    }

    private void TreeItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AllNotesViewModel viewModel
            && sender is TreeViewItem { DataContext: AllNotesTreeItemViewModel item }
            && viewModel.OpenNoteCommand.CanExecute(item))
        {
            viewModel.OpenNoteCommand.Execute(item);
            e.Handled = true;
        }
    }
}
