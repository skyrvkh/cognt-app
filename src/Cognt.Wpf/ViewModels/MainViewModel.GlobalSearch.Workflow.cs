using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void QueueGlobalSearch(bool immediate)
    {
        globalSearchCancellation?.Cancel();
        globalSearchCancellation?.Dispose();
        globalSearchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = globalSearchCancellation.Token;

        string query = GlobalSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearGlobalSearchResults(string.Empty);
            DismissGlobalSearchPopup();
            return;
        }

        GlobalSearchStateMessage = "Searching...";
        GlobalSearchErrorMessage = string.Empty;
        HasGlobalSearchError = false;
        ShowGlobalSearchPopup();
        _ = RunGlobalSearchAsync(query, immediate ? TimeSpan.Zero : TimeSpan.FromMilliseconds(250), cancellationToken);
    }

    private async Task RunGlobalSearchAsync(
        string query,
        TimeSpan debounce,
        CancellationToken cancellationToken)
    {
        try
        {
            if (debounce > TimeSpan.Zero)
            {
                await Task.Delay(debounce, cancellationToken);
            }

            RefreshAvailableCollections();
            IReadOnlyList<GlobalSearchCollectionScope> scopes = CreateGlobalSearchScopes(out int searchableNoteCount);

            if (searchableNoteCount == 0)
            {
                ClearGlobalSearchResults("Create or import notes to make them searchable.");
                return;
            }

            GlobalSearchStateMessage = "Searching...";
            GlobalSearchErrorMessage = string.Empty;
            HasGlobalSearchError = false;
            IsGlobalSearchLoading = true;

            IReadOnlyList<GlobalSearchResultReadModel> results = await Task.Run(
                () => globalSearchService.Search(
                    new GlobalSearchRequest(query, scopes),
                    cancellationToken),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            GlobalSearchResults.Clear();
            foreach (GlobalSearchResultReadModel result in results)
            {
                GlobalSearchResults.Add(result);
            }

            GlobalSearchStateMessage = results.Count == 0
                ? "No notes matched your search."
                : string.Empty;
            HasGlobalSearchError = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GlobalSearchResults.Clear();
            GlobalSearchErrorMessage = $"Search failed: {ex.Message}";
            HasGlobalSearchError = true;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsGlobalSearchLoading = false;
                NotifyGlobalSearchStateChanged();
            }
        }
    }

    private IReadOnlyList<GlobalSearchCollectionScope> CreateGlobalSearchScopes(out int searchableNoteCount)
    {
        List<GlobalSearchCollectionScope> scopes = [];
        searchableNoteCount = 0;

        foreach (Navigation.CollectionOptionViewModel collection in AvailableCollections)
        {
            searchableNoteCount += collection.NoteCount;

            if (!string.IsNullOrWhiteSpace(collection.Path) && Directory.Exists(collection.Path))
            {
                scopes.Add(new GlobalSearchCollectionScope(
                    collection.Id,
                    collection.Name));
            }
        }

        return scopes;
    }

    private void ClearGlobalSearchResults(string message)
    {
        GlobalSearchResults.Clear();
        GlobalSearchStateMessage = message;
        GlobalSearchErrorMessage = string.Empty;
        HasGlobalSearchError = false;
        IsGlobalSearchLoading = false;
        NotifyGlobalSearchStateChanged();
    }

    private void NotifyGlobalSearchStateChanged()
    {
        OnPropertyChanged(nameof(HasGlobalSearchResults));
        OnPropertyChanged(nameof(ShouldShowGlobalSearchResults));
        OnPropertyChanged(nameof(ShouldShowGlobalSearchState));
    }

    private void OpenGlobalSearchResult(object? parameter)
    {
        if (parameter is not GlobalSearchResultReadModel result
            || string.IsNullOrWhiteSpace(result.RelativePath)
            || string.IsNullOrWhiteSpace(result.CollectionId))
        {
            return;
        }

        DismissGlobalSearchPopup();
        string? collectionPath = ResolveCollectionPathFromId(result.CollectionId);
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            return;
        }

        OpenNoteByRelativePath(collectionPath, result.RelativePath, selectInTree: true);
    }
}
