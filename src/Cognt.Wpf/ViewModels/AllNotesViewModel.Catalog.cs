using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private Dictionary<string, KnowledgeNoteListItemReadModel> noteLookupByRelativePath = new(StringComparer.OrdinalIgnoreCase);

    internal async Task LoadAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        currentCollectionId = string.IsNullOrWhiteSpace(collectionId) ? string.Empty : collectionId;
        OnPropertyChanged(nameof(CollectionLabel));
        OnPropertyChanged(nameof(NoteCountLabel));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ResultsSummary));

        if (string.IsNullOrWhiteSpace(currentCollectionId) || !Directory.Exists(currentCollectionId))
        {
            allNotes = [];
            allTreeRoots = [];
            noteLookupByRelativePath = new(StringComparer.OrdinalIgnoreCase);
            VisibleTreeNodes.Clear();
            hasLoadedCatalog = true;
            RebuildFilterOptions([]);
            await ApplyFiltersAsync(CancellationToken.None);
            OnPropertyChanged(nameof(NoteCountLabel));
            OnPropertyChanged(nameof(PageSubtitle));
            OnPropertyChanged(nameof(ResultsSummary));
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            allNotes = await Task.Run(
                () => noteService.LoadNoteCatalog(currentCollectionId),
                cancellationToken);
            noteLookupByRelativePath = allNotes.ToDictionary(note => note.RelativePath, StringComparer.OrdinalIgnoreCase);
            allTreeRoots = await Task.Run(
                () => BuildTreeItems(parentId: null, depth: 0),
                cancellationToken);
            hasLoadedCatalog = true;
            RebuildFilterOptions(allNotes);
            await ApplyFiltersAsync(cancellationToken);
            OnPropertyChanged(nameof(NoteCountLabel));
            OnPropertyChanged(nameof(PageSubtitle));
            OnPropertyChanged(nameof(ResultsSummary));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            allNotes = [];
            allTreeRoots = [];
            noteLookupByRelativePath = new(StringComparer.OrdinalIgnoreCase);
            hasLoadedCatalog = true;
            VisibleNotes.Clear();
            VisibleTreeNodes.Clear();
            ErrorMessage = $"Notes could not be loaded: {ex.Message}";
            HasError = true;
            OnPropertyChanged(nameof(NoteCountLabel));
            OnPropertyChanged(nameof(PageSubtitle));
            OnPropertyChanged(nameof(ResultsSummary));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasVisibleNotes));
            OnPropertyChanged(nameof(ShouldShowEmptyState));
        }
    }

    private void QueueApplyFilters()
    {
        if (!hasLoadedCatalog)
        {
            return;
        }

        applyFiltersCancellation?.Cancel();
        applyFiltersCancellation?.Dispose();
        applyFiltersCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = applyFiltersCancellation.Token;
        _ = ApplyFiltersAsync(cancellationToken);
    }

    private async Task ApplyFiltersAsync(CancellationToken cancellationToken)
    {
        string query = searchQuery.Trim();
        string? selectedTag = NormalizeFilterValue(SelectedTagOption?.Value, AllTagsValue);
        string? selectedType = NormalizeFilterValue(SelectedTypeOption?.Value, AllTypesValue);
        AllNotesSortKey sortKey = SelectedSortOption?.SortKey ?? AllNotesSortKey.ModifiedNewest;
        bool hasActiveFilters = !string.IsNullOrWhiteSpace(query)
            || !string.IsNullOrWhiteSpace(selectedTag)
            || !string.IsNullOrWhiteSpace(selectedType);

        IReadOnlySet<string>? matchingRelativePaths = null;
        if (!string.IsNullOrWhiteSpace(query) && allNotes.Count > 0)
        {
            matchingRelativePaths = await Task.Run(
                () => noteService.SearchNoteCatalog(currentCollectionId, query),
                cancellationToken);
        }

        IReadOnlyList<AllNotesListItemViewModel> filtered = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<KnowledgeNoteListItemReadModel> queryable = allNotes;

            if (matchingRelativePaths is not null)
            {
                queryable = queryable.Where(note => matchingRelativePaths.Contains(note.RelativePath));
            }

            if (!string.IsNullOrWhiteSpace(selectedTag))
            {
                queryable = queryable.Where(note => note.Tags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(selectedType)
                && Enum.TryParse(selectedType, ignoreCase: true, out KnowledgeNoteListItemType noteType))
            {
                queryable = queryable.Where(note => note.Type == noteType);
            }

            IOrderedEnumerable<KnowledgeNoteListItemReadModel> ordered = sortKey switch
            {
                AllNotesSortKey.CreatedOldest => queryable.OrderBy(note => note.CreatedAt).ThenBy(note => note.Title, StringComparer.OrdinalIgnoreCase),
                AllNotesSortKey.CreatedNewest => queryable.OrderByDescending(note => note.CreatedAt).ThenBy(note => note.Title, StringComparer.OrdinalIgnoreCase),
                AllNotesSortKey.ModifiedOldest => queryable.OrderBy(note => note.UpdatedAt).ThenBy(note => note.Title, StringComparer.OrdinalIgnoreCase),
                _ => queryable.OrderByDescending(note => note.UpdatedAt).ThenBy(note => note.Title, StringComparer.OrdinalIgnoreCase)
            };

            return ordered
                .Select(ToListItemViewModel)
                .ToArray();
        }, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        VisibleNotes.Clear();
        foreach (AllNotesListItemViewModel note in filtered)
        {
            VisibleNotes.Add(note);
        }

        ReloadVisibleTreeNodes(
            filtered.Select(note => note.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase),
            hasActiveFilters);
        UpdateEmptyState(filtered.Count);
        OnPropertyChanged(nameof(HasVisibleNotes));
        OnPropertyChanged(nameof(ShouldShowEmptyState));
        OnPropertyChanged(nameof(ResultsSummary));
        openNoteCommand.RaiseCanExecuteChanged();
    }
}
