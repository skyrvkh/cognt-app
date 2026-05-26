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

    private void EditTagsBulk_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestBulkTagEdit(KnowledgeTagOperationKind.Add);
    }

    private void RequestBulkTagEdit(KnowledgeTagOperationKind operation)
    {
        if (DataContext is not AllNotesViewModel viewModel)
        {
            return;
        }

        long[] itemIds = GetSelectedNoteItemIds();
        viewModel.RequestBulkTagEdit(itemIds, operation);
    }

    private long[] GetSelectedNoteItemIds()
    {
        if (AllNotesDataGrid.SelectedItems.Count == 0)
        {
            return [];
        }

        HashSet<long> seenItemIds = [];
        List<long> itemIds = new(AllNotesDataGrid.SelectedItems.Count);
        foreach (object? item in AllNotesDataGrid.SelectedItems)
        {
            if (item is not AllNotesListItemViewModel note || note.ItemId <= 0)
            {
                continue;
            }

            if (seenItemIds.Add(note.ItemId))
            {
                itemIds.Add(note.ItemId);
            }
        }

        return itemIds.ToArray();
    }
}
