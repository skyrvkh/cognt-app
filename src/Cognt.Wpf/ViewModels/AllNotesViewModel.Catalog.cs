using System.Collections.ObjectModel;
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
            allNoteListItems = [];
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
            noteLookupByRelativePath = CreateNoteLookupByRelativePath(allNotes);
            cardCountsBySourceRelativePath = await Task.Run(
                () => LoadCardCountsBySourceRelativePath(currentCollectionId),
                cancellationToken);
            allNoteListItems = CreateListItemViewModels(allNotes);
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
            allNoteListItems = [];
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
        HashSet<string> selectedTagSet = CreateSelectedTagFilterSnapshot();
        HashSet<KnowledgeNoteListItemType> selectedTypeSet = CreateSelectedTypeFilterSnapshot();
        AllNotesSortKey sortKey = SelectedSortOption?.SortKey ?? AllNotesSortKey.ModifiedNewest;
        bool hasActiveFilters = !string.IsNullOrWhiteSpace(query)
            || selectedTagSet.Count > 0
            || selectedTypeSet.Count > 0;
        IReadOnlySet<string>? matchingRelativePaths = null;
        if (!string.IsNullOrWhiteSpace(query) && allNotes.Count > 0)
        {
            matchingRelativePaths = await Task.Run(
                () => noteService.SearchNoteCatalog(currentCollectionId, query),
                cancellationToken);
        }

        FilterApplyResult filtered = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<int> matchingNoteIndexes = new(allNotes.Count);
            for (int allNotesIndex = 0; allNotesIndex < allNotes.Count; allNotesIndex++)
            {
                KnowledgeNoteListItemReadModel note = allNotes[allNotesIndex];
                if (matchingRelativePaths is not null && !matchingRelativePaths.Contains(note.RelativePath))
                {
                    continue;
                }

                if (selectedTagSet.Count > 0 && !HasSelectedTag(note, selectedTagSet))
                {
                    continue;
                }

                if (selectedTypeSet.Count > 0 && !selectedTypeSet.Contains(note.Type))
                {
                    continue;
                }

                matchingNoteIndexes.Add(allNotesIndex);
            }

            SortMatchingNotes(matchingNoteIndexes, allNotes, sortKey);

            HashSet<string>? visibleRelativePaths = hasActiveFilters
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : null;
            AllNotesListItemViewModel[] filteredNotes = new AllNotesListItemViewModel[matchingNoteIndexes.Count];
            for (int index = 0; index < matchingNoteIndexes.Count; index++)
            {
                int allNotesIndex = matchingNoteIndexes[index];
                AllNotesListItemViewModel filteredNote = allNoteListItems[allNotesIndex];
                filteredNotes[index] = filteredNote;
                visibleRelativePaths?.Add(filteredNote.RelativePath);
            }

            return new FilterApplyResult(filteredNotes, visibleRelativePaths);
        }, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        SynchronizeObservableCollection(VisibleNotes, filtered.Notes);
        ReloadVisibleTreeNodes(filtered.VisibleRelativePaths, hasActiveFilters);
        UpdateEmptyState(filtered.Notes.Count);
        OnPropertyChanged(nameof(HasVisibleNotes));
        OnPropertyChanged(nameof(ShouldShowEmptyState));
        OnPropertyChanged(nameof(ResultsSummary));
        openNoteCommand.RaiseCanExecuteChanged();
        showCardsCommand.RaiseCanExecuteChanged();
    }

    private static void SortMatchingNotes(
        List<int> matchingNoteIndexes,
        IReadOnlyList<KnowledgeNoteListItemReadModel> notes,
        AllNotesSortKey sortKey)
    {
        matchingNoteIndexes.Sort((leftIndex, rightIndex) => CompareNotes(
            notes[leftIndex],
            leftIndex,
            notes[rightIndex],
            rightIndex,
            sortKey));
    }

    private static int CompareNotes(
        KnowledgeNoteListItemReadModel leftNote,
        int leftOriginalIndex,
        KnowledgeNoteListItemReadModel rightNote,
        int rightOriginalIndex,
        AllNotesSortKey sortKey)
    {
        int comparison = sortKey switch
        {
            AllNotesSortKey.CreatedOldest => leftNote.CreatedAt.CompareTo(rightNote.CreatedAt),
            AllNotesSortKey.CreatedNewest => rightNote.CreatedAt.CompareTo(leftNote.CreatedAt),
            AllNotesSortKey.ModifiedOldest => leftNote.UpdatedAt.CompareTo(rightNote.UpdatedAt),
            _ => rightNote.UpdatedAt.CompareTo(leftNote.UpdatedAt)
        };

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.OrdinalIgnoreCase.Compare(leftNote.Title, rightNote.Title);
        return comparison != 0
            ? comparison
            : leftOriginalIndex.CompareTo(rightOriginalIndex);
    }

    private static Dictionary<string, KnowledgeNoteListItemReadModel> CreateNoteLookupByRelativePath(
        IReadOnlyList<KnowledgeNoteListItemReadModel> notes)
    {
        Dictionary<string, KnowledgeNoteListItemReadModel> lookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (KnowledgeNoteListItemReadModel note in notes)
        {
            lookup[note.RelativePath] = note;
        }

        return lookup;
    }

    private static bool HasSelectedTag(
        KnowledgeNoteListItemReadModel note,
        HashSet<string> selectedTagSet)
    {
        foreach (string tag in note.Tags)
        {
            if (selectedTagSet.Contains(tag))
            {
                return true;
            }
        }

        return false;
    }

    private AllNotesListItemViewModel[] CreateListItemViewModels(
        IReadOnlyList<KnowledgeNoteListItemReadModel> notes)
    {
        AllNotesListItemViewModel[] listItems = new AllNotesListItemViewModel[notes.Count];
        for (int index = 0; index < notes.Count; index++)
        {
            listItems[index] = ToListItemViewModel(notes[index]);
        }

        return listItems;
    }

    private Dictionary<string, int> LoadCardCountsBySourceRelativePath(string collectionId)
    {
        try
        {
            Dictionary<string, int> countsBySourceRelativePath = new(StringComparer.OrdinalIgnoreCase);
            foreach (var card in flashcardCardsQuery.Execute(new FlashcardCardsQueryRequest(collectionId, Limit: 1000)))
            {
                if (string.IsNullOrWhiteSpace(card.SourceRelPath))
                {
                    continue;
                }

                string sourceRelativePath = NormalizeSourceRelativePath(card.SourceRelPath!);
                countsBySourceRelativePath[sourceRelativePath] = countsBySourceRelativePath.TryGetValue(sourceRelativePath, out int count)
                    ? count + 1
                    : 1;
            }

            return countsBySourceRelativePath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SynchronizeObservableCollection<T>(
        ObservableCollection<T> collection,
        IReadOnlyList<T> nextItems)
        where T : class
    {
        int index = 0;
        while (index < nextItems.Count)
        {
            T nextItem = nextItems[index];
            if (index < collection.Count && ReferenceEquals(collection[index], nextItem))
            {
                index++;
                continue;
            }

            int existingIndex = FindExistingIndex(collection, nextItem, index + 1);
            if (existingIndex >= 0)
            {
                collection.Move(existingIndex, index);
            }
            else if (index < collection.Count)
            {
                collection.Insert(index, nextItem);
            }
            else
            {
                collection.Add(nextItem);
            }

            index++;
        }

        while (collection.Count > nextItems.Count)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    private static int FindExistingIndex<T>(
        ObservableCollection<T> collection,
        T item,
        int startIndex)
        where T : class
    {
        for (int index = startIndex; index < collection.Count; index++)
        {
            if (ReferenceEquals(collection[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    private readonly record struct FilterApplyResult(
        IReadOnlyList<AllNotesListItemViewModel> Notes,
        IReadOnlySet<string>? VisibleRelativePaths);
}
