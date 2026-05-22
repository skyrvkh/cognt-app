using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private string? requestedSourceFilter;
    private bool resetFiltersForRequestedSource;

    internal void RequestSourceFilter(string? sourceRelPath)
    {
        requestedSourceFilter = string.IsNullOrWhiteSpace(sourceRelPath)
            ? CardsPresentationFactory.AllSourcesValue
            : sourceRelPath.Replace('\\', '/').Trim('/');
        resetFiltersForRequestedSource = true;
    }

    public FlashcardCardsQueryRequest CreateCardsRequest(
        string collectionId,
        bool includeDeckFilter = true)
    {
        // TODO: Text search should be added here when a card search service exists.
        return new FlashcardCardsQueryRequest(
            collectionId,
            includeDeckFilter ? SelectedDeckFilter?.Id : null,
            GetSelectedFilterKind(),
            GetSelectedSourceRelPath(),
            Tags: GetSelectedTags());
    }

    private void ConfigureFilterOptions(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        string? previousStatus = resetFiltersForRequestedSource
            ? FlashcardCardsFilterKind.All.ToString()
            : SelectedStatusFilter?.Value;
        string? previousSource = requestedSourceFilter ?? SelectedSourceFilter?.Value;
        string[] previousTags = resetFiltersForRequestedSource
            ? []
            : GetSelectedTags();

        CardsFilterOptionsState options = CardsPresentationFactory.CreateFilterOptions(
            cards,
            decks,
            nowUtc,
            sourceTitlesByRelPath);
        CardFilterDeckRows = options.DeckRows;
        CardFilterStatusRows = options.StatusRows;
        CardFilterSourceRows = options.SourceRows;
        CardFilterTagRows = options.TagRows;

        SelectedDeckFilter = CardFilterDeckRows.FirstOrDefault();
        SelectedStatusFilter = FindOption(CardFilterStatusRows, previousStatus) ?? CardFilterStatusRows.FirstOrDefault();
        SelectedSourceFilter = FindOption(CardFilterSourceRows, previousSource) ?? CardFilterSourceRows.FirstOrDefault();
        ConfigureSelectedTagFilters(previousTags);
        requestedSourceFilter = null;
        resetFiltersForRequestedSource = false;
    }

    private void ResetFilterSelections()
    {
        SelectedDeckFilter = CardFilterDeckRows.FirstOrDefault();
        SelectedStatusFilter = CardFilterStatusRows.FirstOrDefault();
        SelectedSourceFilter = CardFilterSourceRows.FirstOrDefault();
        ConfigureSelectedTagFilters([]);
    }

    private FlashcardCardsFilterKind GetSelectedFilterKind()
    {
        return Enum.TryParse(SelectedStatusFilter?.Value, out FlashcardCardsFilterKind filter)
            ? filter
            : FlashcardCardsFilterKind.All;
    }

    private string? GetSelectedSourceRelPath()
    {
        return string.Equals(SelectedSourceFilter?.Value, CardsPresentationFactory.AllSourcesValue, StringComparison.Ordinal)
            ? null
            : SelectedSourceFilter?.Value;
    }

    private string[] GetSelectedTags()
    {
        return CardFilterTagRows
            .Where(row => row.IsSelected && !string.Equals(row.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal))
            .Select(row => row.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private void UpdateActiveFilterSummary()
    {
        ActiveFilterSummary = CardsPresentationFactory.CreateActiveFilterSummary(
            SelectedDeckFilter,
            SelectedStatusFilter,
            SelectedSourceFilter,
            GetSelectedFilterKind(),
            GetSelectedSourceRelPath(),
            GetSelectedTags());
    }

    private void ConfigureSelectedTagFilters(IReadOnlyList<string> selectedTags)
    {
        bool hasSelectedTags = selectedTags.Count > 0;
        foreach (CardFilterOptionRow row in CardFilterTagRows)
        {
            row.PropertyChanged -= CardFilterTagRow_PropertyChanged;
            row.IsSelected = string.Equals(row.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal)
                ? !hasSelectedTags
                : row.Value is not null && selectedTags.Contains(row.Value, StringComparer.OrdinalIgnoreCase);
            row.PropertyChanged += CardFilterTagRow_PropertyChanged;
        }

        UpdateActiveFilterSummary();
    }

    private void CardFilterTagRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CardFilterOptionRow.IsSelected)
            || sender is not CardFilterOptionRow changed)
        {
            return;
        }

        if (string.Equals(changed.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal))
        {
            if (changed.IsSelected)
            {
                foreach (CardFilterOptionRow row in CardFilterTagRows.Where(row => row != changed))
                {
                    row.IsSelected = false;
                }
            }
            else if (!CardFilterTagRows.Any(row => row.IsSelected))
            {
                changed.IsSelected = true;
            }

            UpdateActiveFilterSummary();
            return;
        }

        CardFilterOptionRow? allTags = FindOption(CardFilterTagRows, CardsPresentationFactory.AllTagsValue);
        if (changed.IsSelected && allTags is not null)
        {
            allTags.IsSelected = false;
        }
        else if (!CardFilterTagRows.Any(row =>
                     row.IsSelected
                     && !string.Equals(row.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal))
                 && allTags is not null)
        {
            allTags.IsSelected = true;
        }

        UpdateActiveFilterSummary();
    }

    private static CardFilterOptionRow? FindOption(
        IEnumerable<CardFilterOptionRow> rows,
        string? value)
    {
        return rows.FirstOrDefault(row => string.Equals(row.Value, value, StringComparison.OrdinalIgnoreCase));
    }
}
