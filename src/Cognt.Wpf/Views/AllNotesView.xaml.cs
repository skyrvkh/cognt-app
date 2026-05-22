using System.Windows.Controls;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels;

namespace CogntApp.Views;

public partial class AllNotesView : UserControl
{
    public AllNotesView()
    {
        InitializeComponent();
    }

    private void AddTagsBulk_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestBulkTagEdit(KnowledgeTagOperationKind.Add);
    }

    private void RemoveTagsBulk_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestBulkTagEdit(KnowledgeTagOperationKind.Remove);
    }

    private void RequestBulkTagEdit(KnowledgeTagOperationKind operation)
    {
        if (DataContext is not AllNotesViewModel viewModel)
        {
            return;
        }

        long[] itemIds = AllNotesDataGrid.SelectedItems
            .OfType<AllNotesListItemViewModel>()
            .Select(note => note.ItemId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        viewModel.RequestBulkTagEdit(itemIds, operation);
    }
}
