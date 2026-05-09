using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    public FlashcardCardsQueryRequest CreateCardsRequest(
        string collectionId,
        bool includeDeckFilter = true)
    {
        // TODO: Suspended/hidden cards are not represented in flashcard_card.
        // TODO: Text search should be added here when a card search service exists.
        return new FlashcardCardsQueryRequest(
            collectionId,
            includeDeckFilter ? SelectedDeckFilter?.Id : null,
            GetSelectedFilterKind(),
            GetSelectedSourceRelPath(),
            GetSelectedTag());
    }

    private void ConfigureFilterOptions(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        string? previousStatus = SelectedStatusFilter?.Value;
        string? previousSource = SelectedSourceFilter?.Value;
        string? previousTag = SelectedTagFilter?.Value;

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
        SelectedTagFilter = FindOption(CardFilterTagRows, previousTag) ?? CardFilterTagRows.FirstOrDefault();
    }

    private void ResetFilterSelections()
    {
        SelectedDeckFilter = CardFilterDeckRows.FirstOrDefault();
        SelectedStatusFilter = CardFilterStatusRows.FirstOrDefault();
        SelectedSourceFilter = CardFilterSourceRows.FirstOrDefault();
        SelectedTagFilter = CardFilterTagRows.FirstOrDefault();
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

    private string? GetSelectedTag()
    {
        return string.Equals(SelectedTagFilter?.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal)
            ? null
            : SelectedTagFilter?.Value;
    }

    private void UpdateActiveFilterSummary()
    {
        ActiveFilterSummary = CardsPresentationFactory.CreateActiveFilterSummary(
            SelectedDeckFilter,
            SelectedStatusFilter,
            SelectedSourceFilter,
            SelectedTagFilter,
            GetSelectedFilterKind(),
            GetSelectedSourceRelPath(),
            GetSelectedTag());
    }

    private static CardFilterOptionRow? FindOption(
        IEnumerable<CardFilterOptionRow> rows,
        string? value)
    {
        return rows.FirstOrDefault(row => string.Equals(row.Value, value, StringComparison.OrdinalIgnoreCase));
    }
}
