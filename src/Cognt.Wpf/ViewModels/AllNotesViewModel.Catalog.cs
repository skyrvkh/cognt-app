using System.Collections.ObjectModel;
using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Microsoft.Extensions.Logging;

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
            logger.LogError(ex, "All notes catalog load failed. CollectionId={CollectionId}", currentCollectionId);
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

        AllNotesFilterApplyResult filtered = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return AllNotesFilterEngine.Apply(
                allNotes,
                allNoteListItems,
                matchingRelativePaths,
                selectedTagSet,
                selectedTypeSet,
                sortKey,
                hasActiveFilters);
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

                string sourceRelativePath = AllNotesPresentationFormatter.NormalizeSourceRelativePath(card.SourceRelPath!);
                countsBySourceRelativePath[sourceRelativePath] = countsBySourceRelativePath.TryGetValue(sourceRelativePath, out int count)
                    ? count + 1
                    : 1;
            }

            return countsBySourceRelativePath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            logger.LogWarning(ex, "All notes card count load failed; continuing without card counts. CollectionId={CollectionId}", collectionId);
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

}
