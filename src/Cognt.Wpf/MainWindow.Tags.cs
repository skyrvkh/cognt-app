using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private void AddTagsTreeItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestNavigationTagEdit(KnowledgeTagOperationKind.Add);
    }

    private void RemoveTagsTreeItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestNavigationTagEdit(KnowledgeTagOperationKind.Remove);
    }

    private void RequestNavigationTagEdit(KnowledgeTagOperationKind operation)
    {
        NoteTreeNodeViewModel? node = noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel);
        if (node is not { Id: > 0, IsPlaceholder: false })
        {
            return;
        }

        _ = ShowTagEditDialogAsync(
            new TagEditRequestedEventArgs(
                [node.Id],
                operation,
                node.Title));
    }

    private async void AllNotesViewModel_TagEditRequested(object? sender, TagEditRequestedEventArgs e)
    {
        await ShowTagEditDialogAsync(e);
    }

    private async Task ShowTagEditDialogAsync(TagEditRequestedEventArgs e)
    {
        try
        {
            TagEditDialog dialog = new(
                e.Title,
                e.Operation,
                viewModel.LoadAvailableTags(),
                viewModel.LoadAssignedTags(e.ItemIds))
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            KnowledgeTagOperationResult result = viewModel.ApplyTags(
                e.ItemIds,
                dialog.SelectedTags,
                dialog.IncludeDescendants,
                e.Operation);

            if (!string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath))
            {
                await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
            }

            shellDialogs.ShowInfo(
                "Tags Updated",
                FormatTagOperationResult(result));
        }
        catch (Exception ex)
        {
            shellDialogs.ShowWarning("Tags", $"Tags could not be updated:{Environment.NewLine}{ex.Message}");
        }
    }

    private static string FormatTagOperationResult(KnowledgeTagOperationResult result)
    {
        string operation = result.Operation == KnowledgeTagOperationKind.Add ? "assigned" : "removed";
        string scope = result.IncludedDescendants ? "including descendants" : "selected item scope";
        string tagLabel = result.Tags.Count == 1
            ? $"'{result.Tags[0]}'"
            : $"{result.Tags.Count} tags";

        return $"{tagLabel} {operation} for {result.AffectedItemCount} item(s), {scope}.";
    }
}
