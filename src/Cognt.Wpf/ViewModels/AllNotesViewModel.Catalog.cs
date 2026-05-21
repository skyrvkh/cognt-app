using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

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
            cardCountsBySourceRelativePath = new(StringComparer.OrdinalIgnoreCase);
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
            cardCountsBySourceRelativePath = await Task.Run(
                () => LoadCardCountsBySourceRelativePath(currentCollectionId),
                cancellationToken);
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
            cardCountsBySourceRelativePath = new(StringComparer.OrdinalIgnoreCase);
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
        string[] selectedTags = GetSelectedFilterValues(TagOptions, AllTagsValue);
        string[] selectedTypes = GetSelectedFilterValues(TypeOptions, AllTypesValue);
        AllNotesSortKey sortKey = SelectedSortOption?.SortKey ?? AllNotesSortKey.ModifiedNewest;
        bool hasActiveFilters = !string.IsNullOrWhiteSpace(query)
            || selectedTags.Length > 0
            || selectedTypes.Length > 0;

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

            if (selectedTags.Length > 0)
            {
                queryable = queryable.Where(note => note.Tags.Any(tag => selectedTags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            }

            if (selectedTypes.Length > 0)
            {
                HashSet<KnowledgeNoteListItemType> selectedTypeValues = selectedTypes
                    .Select(type => Enum.TryParse(type, ignoreCase: true, out KnowledgeNoteListItemType noteType)
                        ? (KnowledgeNoteListItemType?)noteType
                        : null)
                    .OfType<KnowledgeNoteListItemType>()
                    .ToHashSet();

                if (selectedTypeValues.Count > 0)
                {
                    queryable = queryable.Where(note => selectedTypeValues.Contains(note.Type));
                }
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

    private Dictionary<string, int> LoadCardCountsBySourceRelativePath(string collectionId)
    {
        try
        {
            return flashcardCardsQuery
                .Execute(new FlashcardCardsQueryRequest(collectionId, Limit: 1000))
                .Where(card => !string.IsNullOrWhiteSpace(card.SourceRelPath))
                .GroupBy(card => NormalizeSourceRelativePath(card.SourceRelPath!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
