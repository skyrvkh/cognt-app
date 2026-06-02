using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectOnlyAllFilterOption(TagOptions, AllTagsValue);
        SelectOnlyAllFilterOption(TypeOptions, AllTypesValue);
        SelectedSortOption = SortOptions.FirstOrDefault();
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private bool CanOpenNoteFromCommand(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(currentCollectionId))
        {
            return false;
        }

        return parameter switch
        {
            AllNotesListItemViewModel note => !string.IsNullOrWhiteSpace(note.RelativePath),
            AllNotesTreeItemViewModel treeItem => treeItem.CanOpen,
            _ => false
        };
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        string? relativePath = parameter switch
        {
            AllNotesListItemViewModel note => note.RelativePath,
            AllNotesTreeItemViewModel treeItem when treeItem.CanOpen => treeItem.RelativePath,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            OpenNoteRequested?.Invoke(this, new OpenAllNotesRequestedEventArgs(relativePath));
            return;
        }

    }

    private bool CanShowCardsFromCommand(object? parameter)
    {
        return parameter switch
        {
            AllNotesListItemViewModel note => note.HasCards,
            AllNotesTreeItemViewModel treeItem => treeItem.HasCards,
            _ => false
        };
    }

    private void ShowCardsFromCommand(object? parameter)
    {
        (string? relativePath, string? title) = parameter switch
        {
            AllNotesListItemViewModel note when note.HasCards => (note.RelativePath, note.Title),
            AllNotesTreeItemViewModel treeItem when treeItem.HasCards => (treeItem.RelativePath, treeItem.Title),
            _ => (null, null)
        };

        if (!string.IsNullOrWhiteSpace(relativePath) && !string.IsNullOrWhiteSpace(title))
        {
            ShowCardsRequested?.Invoke(this, new OpenNoteCardsRequestedEventArgs(relativePath, title));
            return;
        }

    }

    internal void RequestBulkTagEdit(
        IReadOnlyList<long> itemIds,
        KnowledgeTagOperationKind operation)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        TagEditRequested?.Invoke(
            this,
            new TagEditRequestedEventArgs(
                itemIds,
                operation,
                itemIds.Count == 1 ? "1 item" : $"{itemIds.Count} selected items"));
    }

    private bool CanRequestTagEdit(object? parameter)
    {
        return GetTagTarget(parameter) is { Id: > 0 };
    }

    private void RequestTagEdit(object? parameter, KnowledgeTagOperationKind operation)
    {
        (long id, string title) = GetTagTarget(parameter) ?? default;
        if (id <= 0)
        {
            return;
        }

        TagEditRequested?.Invoke(
            this,
            new TagEditRequestedEventArgs([id], operation, title));
    }

    private static (long Id, string Title)? GetTagTarget(object? parameter)
    {
        return parameter switch
        {
            AllNotesListItemViewModel note => (note.ItemId, note.Title),
            AllNotesTreeItemViewModel treeItem => (treeItem.Id, treeItem.Title),
            _ => null
        };
    }
}
