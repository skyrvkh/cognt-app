using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private string? requestedSourceFilter;
    private bool resetFiltersForRequestedSource;
    private bool isConfiguringSingleSelectFilters;

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
        return new FlashcardCardsQueryRequest(
            collectionId,
            includeDeckFilter ? SelectedDeckFilter?.Id : null,
            GetSelectedFilterKind(),
            GetSelectedSourceRelPath(),
            Tags: GetSelectedTags(),
            SearchText: SearchText);
    }

    private void ConfigureFilterOptions(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        long? previousDeckId = SelectedDeckFilter?.Id;
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

        ConfigureSelectedDeckFilter(previousDeckId);
        ConfigureSelectedStatusFilter(previousStatus);
        ConfigureSelectedSourceFilter(previousSource);
        ConfigureSelectedTagFilters(previousTags);
        requestedSourceFilter = null;
        resetFiltersForRequestedSource = false;
    }

    private void ResetFilterSelections()
    {
        ConfigureSelectedDeckFilter(null);
        ConfigureSelectedStatusFilter(CardFilterStatusRows.FirstOrDefault()?.Value);
        ConfigureSelectedSourceFilter(CardFilterSourceRows.FirstOrDefault()?.Value);
        SearchText = string.Empty;
        DeckFilterSearchText = string.Empty;
        SourceFilterSearchText = string.Empty;
        TagFilterSearchText = string.Empty;
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
            GetSelectedTags(),
            SearchText);
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

    private void ConfigureSelectedDeckFilter(long? selectedDeckId)
    {
        CardFilterDeckRow? selected = selectedDeckId.HasValue
            ? CardFilterDeckRows.FirstOrDefault(row => row.Id == selectedDeckId.Value)
            : CardFilterDeckRows.FirstOrDefault(row => !row.Id.HasValue);
        selected ??= CardFilterDeckRows.FirstOrDefault();

        isConfiguringSingleSelectFilters = true;
        try
        {
            foreach (CardFilterDeckRow row in CardFilterDeckRows)
            {
                row.PropertyChanged -= CardFilterDeckRow_PropertyChanged;
                row.IsSelected = ReferenceEquals(row, selected);
                row.PropertyChanged += CardFilterDeckRow_PropertyChanged;
            }

            SelectedDeckFilter = selected;
        }
        finally
        {
            isConfiguringSingleSelectFilters = false;
        }

        OnPropertyChanged(nameof(VisibleCardFilterDeckRows));
        UpdateActiveFilterSummary();
    }

    private void CardFilterDeckRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isConfiguringSingleSelectFilters
            || e.PropertyName != nameof(CardFilterDeckRow.IsSelected)
            || sender is not CardFilterDeckRow changed)
        {
            return;
        }

        if (changed.IsSelected)
        {
            ConfigureSelectedDeckFilter(changed.Id);
            return;
        }

        if (!CardFilterDeckRows.Any(row => row.IsSelected))
        {
            ConfigureSelectedDeckFilter(null);
        }
    }

    private void ConfigureSelectedStatusFilter(string? selectedValue)
    {
        CardFilterOptionRow? selected = FindOption(CardFilterStatusRows, selectedValue)
            ?? CardFilterStatusRows.FirstOrDefault();
        isConfiguringSingleSelectFilters = true;
        try
        {
            foreach (CardFilterOptionRow row in CardFilterStatusRows)
            {
                row.PropertyChanged -= CardFilterStatusRow_PropertyChanged;
                row.IsSelected = ReferenceEquals(row, selected);
                row.PropertyChanged += CardFilterStatusRow_PropertyChanged;
            }

            SelectedStatusFilter = selected;
        }
        finally
        {
            isConfiguringSingleSelectFilters = false;
        }

        UpdateActiveFilterSummary();
    }

    private void ConfigureSelectedSourceFilter(string? selectedValue)
    {
        CardFilterOptionRow? selected = FindOption(CardFilterSourceRows, selectedValue)
            ?? CardFilterSourceRows.FirstOrDefault();
        isConfiguringSingleSelectFilters = true;
        try
        {
            foreach (CardFilterOptionRow row in CardFilterSourceRows)
            {
                row.PropertyChanged -= CardFilterSourceRow_PropertyChanged;
                row.IsSelected = ReferenceEquals(row, selected);
                row.PropertyChanged += CardFilterSourceRow_PropertyChanged;
            }

            SelectedSourceFilter = selected;
        }
        finally
        {
            isConfiguringSingleSelectFilters = false;
        }

        OnPropertyChanged(nameof(VisibleCardFilterSourceRows));
        UpdateActiveFilterSummary();
    }

    private void CardFilterStatusRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isConfiguringSingleSelectFilters
            || e.PropertyName != nameof(CardFilterOptionRow.IsSelected)
            || sender is not CardFilterOptionRow changed)
        {
            return;
        }

        if (changed.IsSelected)
        {
            ConfigureSelectedStatusFilter(changed.Value);
            return;
        }

        if (!CardFilterStatusRows.Any(row => row.IsSelected))
        {
            ConfigureSelectedStatusFilter(CardFilterStatusRows.FirstOrDefault()?.Value);
        }
    }

    private void CardFilterSourceRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isConfiguringSingleSelectFilters
            || e.PropertyName != nameof(CardFilterOptionRow.IsSelected)
            || sender is not CardFilterOptionRow changed)
        {
            return;
        }

        if (changed.IsSelected)
        {
            ConfigureSelectedSourceFilter(changed.Value);
            return;
        }

        if (!CardFilterSourceRows.Any(row => row.IsSelected))
        {
            ConfigureSelectedSourceFilter(CardFilterSourceRows.FirstOrDefault()?.Value);
        }
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

            OnPropertyChanged(nameof(VisibleCardFilterTagRows));
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

        OnPropertyChanged(nameof(VisibleCardFilterTagRows));
        UpdateActiveFilterSummary();
    }

    private static CardFilterOptionRow? FindOption(
        IEnumerable<CardFilterOptionRow> rows,
        string? value)
    {
        return rows.FirstOrDefault(row => string.Equals(row.Value, value, StringComparison.OrdinalIgnoreCase));
    }
}
