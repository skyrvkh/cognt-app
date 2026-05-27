using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private string? requestedSourceFilter;
    private bool resetFiltersForRequestedSource;
    private bool isConfiguringSingleSelectFilters;
    private bool isConfiguringTagFilters;

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

    private void ConfigureFilterOptions(CardsFilterOptionsState options)
    {
        long? previousDeckId = SelectedDeckFilter?.Id;
        string? previousStatus = resetFiltersForRequestedSource
            ? FlashcardCardsFilterKind.All.ToString()
            : SelectedStatusFilter?.Value;
        string? previousSource = requestedSourceFilter ?? SelectedSourceFilter?.Value;
        string[] previousTags = resetFiltersForRequestedSource
            ? []
            : GetSelectedTags();

        isUpdatingFilterConfiguration = true;
        try
        {
            SynchronizeCardFilterDeckRows(options.DeckRows);
            SynchronizeCardFilterStatusRows(options.StatusRows);
            SynchronizeCardFilterSourceRows(options.SourceRows);
            SynchronizeCardFilterTagRows(options.TagRows);

            ConfigureSelectedDeckFilter(previousDeckId);
            ConfigureSelectedStatusFilter(previousStatus);
            ConfigureSelectedSourceFilter(previousSource);
            ConfigureSelectedTagFilters(previousTags);
            requestedSourceFilter = null;
            resetFiltersForRequestedSource = false;
        }
        finally
        {
            isUpdatingFilterConfiguration = false;
        }

        RefreshAllFilterPresentationState();
    }

    private void ResetFilterSelections()
    {
        isUpdatingFilterConfiguration = true;
        try
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
        finally
        {
            isUpdatingFilterConfiguration = false;
        }

        RefreshAllFilterPresentationState();
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
        return selectedFilterTags;
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
        SetSelectedTags(selectedTags);
        bool hasSelectedTags = selectedFilterTagSet.Count > 0;
        isConfiguringTagFilters = true;
        try
        {
            foreach (CardFilterOptionRow row in CardFilterTagRows)
            {
                row.PropertyChanged -= CardFilterTagRow_PropertyChanged;
                row.IsSelected = string.Equals(row.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal)
                    ? !hasSelectedTags
                    : IsSelectedTagValue(row.Value);
                row.PropertyChanged += CardFilterTagRow_PropertyChanged;
            }
        }
        finally
        {
            isConfiguringTagFilters = false;
        }

        if (!isUpdatingFilterConfiguration)
        {
            RefreshTagFilterPresentationState();
        }
    }

    private static string? NormalizeTagValue(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
    }

    private void SetSelectedTags(IReadOnlyList<string> selectedTags)
    {
        selectedFilterTagSet.Clear();
        if (selectedTags.Count == 0)
        {
            selectedFilterTags = [];
            return;
        }

        List<string>? normalizedTags = null;
        foreach (string tag in selectedTags)
        {
            string? normalizedTag = NormalizeTagValue(tag);
            if (normalizedTag is null || !selectedFilterTagSet.Add(normalizedTag))
            {
                continue;
            }

            normalizedTags ??= new List<string>(selectedTags.Count);
            normalizedTags.Add(normalizedTag);
        }

        selectedFilterTags = normalizedTags?.ToArray() ?? [];
    }

    private bool IsSelectedTagValue(string? tag)
    {
        string? normalizedTag = NormalizeTagValue(tag);
        return normalizedTag is not null && selectedFilterTagSet.Contains(normalizedTag);
    }

    private void AddSelectedTag(string? tag)
    {
        string? normalizedTag = NormalizeTagValue(tag);
        if (normalizedTag is null || !selectedFilterTagSet.Add(normalizedTag))
        {
            return;
        }

        string[] currentTags = selectedFilterTags;
        string[] updatedTags = new string[currentTags.Length + 1];
        Array.Copy(currentTags, updatedTags, currentTags.Length);
        updatedTags[currentTags.Length] = normalizedTag;
        selectedFilterTags = updatedTags;
    }

    private void RemoveSelectedTag(string? tag)
    {
        string? normalizedTag = NormalizeTagValue(tag);
        if (normalizedTag is null || !selectedFilterTagSet.Remove(normalizedTag))
        {
            return;
        }

        if (selectedFilterTags.Length == 0)
        {
            return;
        }

        List<string>? remainingTags = null;
        foreach (string selectedTag in selectedFilterTags)
        {
            if (string.Equals(selectedTag, normalizedTag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            remainingTags ??= new List<string>(selectedFilterTags.Length - 1);
            remainingTags.Add(selectedTag);
        }

        selectedFilterTags = remainingTags?.ToArray() ?? [];
    }

    private void ClearSelectedTags()
    {
        selectedFilterTagSet.Clear();
        selectedFilterTags = [];
    }

    private void ConfigureSelectedDeckFilter(long? selectedDeckId)
    {
        CardFilterDeckRow? selected = FindDeckRow(CardFilterDeckRows, selectedDeckId);

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

        if (SelectedDeckFilter is null || !SelectedDeckFilter.IsSelected)
        {
            ConfigureSelectedDeckFilter(null);
        }
    }

    private void ConfigureSelectedStatusFilter(string? selectedValue)
    {
        CardFilterOptionRow? selected = FindOption(CardFilterStatusRows, selectedValue);
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
    }

    private void ConfigureSelectedSourceFilter(string? selectedValue)
    {
        CardFilterOptionRow? selected = FindOption(CardFilterSourceRows, selectedValue);
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

        if (SelectedStatusFilter is null || !SelectedStatusFilter.IsSelected)
        {
            ConfigureSelectedStatusFilter(null);
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

        if (SelectedSourceFilter is null || !SelectedSourceFilter.IsSelected)
        {
            ConfigureSelectedSourceFilter(null);
        }
    }

    private void CardFilterTagRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isConfiguringTagFilters
            || e.PropertyName != nameof(CardFilterOptionRow.IsSelected)
            || sender is not CardFilterOptionRow changed)
        {
            return;
        }

        isConfiguringTagFilters = true;
        try
        {
            if (string.Equals(changed.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal))
            {
                if (changed.IsSelected)
                {
                    ClearSelectedTags();
                    foreach (CardFilterOptionRow row in CardFilterTagRows)
                    {
                        if (!ReferenceEquals(row, changed) && row.IsSelected)
                        {
                            row.IsSelected = false;
                        }
                    }
                }
                else if (selectedFilterTagSet.Count == 0)
                {
                    changed.IsSelected = true;
                }
            }
            else
            {
                CardFilterOptionRow? allTags = FindOption(CardFilterTagRows, CardsPresentationFactory.AllTagsValue);
                if (changed.IsSelected)
                {
                    AddSelectedTag(changed.Value);
                    if (allTags is not null)
                    {
                        allTags.IsSelected = false;
                    }
                }
                else
                {
                    RemoveSelectedTag(changed.Value);
                    if (selectedFilterTagSet.Count == 0 && allTags is not null)
                    {
                        allTags.IsSelected = true;
                    }
                }
            }
        }
        finally
        {
            isConfiguringTagFilters = false;
        }

        RefreshTagFilterPresentationState();
    }

    private static CardFilterOptionRow? FindOption(
        IReadOnlyList<CardFilterOptionRow> rows,
        string? value)
    {
        CardFilterOptionRow? firstRow = null;
        foreach (CardFilterOptionRow row in rows)
        {
            firstRow ??= row;
            if (string.Equals(row.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return firstRow;
    }

    private static CardFilterDeckRow? FindDeckRow(
        IReadOnlyList<CardFilterDeckRow> rows,
        long? selectedDeckId)
    {
        CardFilterDeckRow? firstRow = null;
        CardFilterDeckRow? allDecksRow = null;
        foreach (CardFilterDeckRow row in rows)
        {
            firstRow ??= row;
            if (!row.Id.HasValue)
            {
                allDecksRow ??= row;
            }

            if (selectedDeckId.HasValue)
            {
                if (row.Id == selectedDeckId.Value)
                {
                    return row;
                }
            }
            else if (!row.Id.HasValue)
            {
                return row;
            }
        }

        return allDecksRow ?? firstRow;
    }


}
