using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private void EditTagsTreeItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RequestNavigationTagEdit(KnowledgeTagOperationKind.Add);
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

            KnowledgeTagOperationResult? addResult = null;
            KnowledgeTagOperationResult? removeResult = null;
            if (e.Operation == KnowledgeTagOperationKind.Add)
            {
                if (dialog.SelectedTags.Count > 0)
                {
                    addResult = viewModel.ApplyTags(
                        e.ItemIds,
                        dialog.SelectedTags,
                        dialog.IncludeDescendants,
                        KnowledgeTagOperationKind.Add);
                }

                if (dialog.RemovedTags.Count > 0)
                {
                    removeResult = viewModel.ApplyTags(
                        e.ItemIds,
                        dialog.RemovedTags,
                        dialog.IncludeDescendants,
                        KnowledgeTagOperationKind.Remove);
                }
            }
            else
            {
                removeResult = viewModel.ApplyTags(
                    e.ItemIds,
                    dialog.SelectedTags,
                    dialog.IncludeDescendants,
                    e.Operation);
            }

            if (!string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath))
            {
                await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
            }

            shellDialogs.ShowInfo(
                "Tags Updated",
                FormatTagEditResult(addResult, removeResult));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Tag edit failed. Operation={Operation} ItemCount={ItemCount} Title={Title}",
                e.Operation,
                e.ItemIds.Count,
                e.Title);
            shellDialogs.ShowWarning("Tags", $"Tags could not be updated:{Environment.NewLine}{ex.Message}");
        }
    }

    private static string FormatTagEditResult(
        KnowledgeTagOperationResult? addResult,
        KnowledgeTagOperationResult? removeResult)
    {
        string[] messages = new[] { addResult, removeResult }
            .Where(result => result is not null)
            .Select(result => FormatTagOperationResult(result!))
            .ToArray();

        return messages.Length == 0
            ? "No tag changes were applied."
            : string.Join(Environment.NewLine, messages);
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
