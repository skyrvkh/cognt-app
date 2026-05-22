using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class CardsPresentationFactory
{
    internal static CardsFilterOptionsState CreateFilterOptions(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, string>? sourceTitlesByRelPath = null)
    {
        int totalCardCount = decks.Sum(deck => deck.TotalCards);

        return new CardsFilterOptionsState(
            new[] { new CardFilterDeckRow("All Decks", NumericTextFormatter.Format(totalCardCount)) }
                .Concat(decks.Select(deck => new CardFilterDeckRow(
                    deck.Name,
                    NumericTextFormatter.Format(deck.TotalCards),
                    deck.Id)))
                .ToArray(),
            CreateStatusFilters(cards, nowUtc),
            CreateSourceFilters(cards, sourceTitlesByRelPath ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            CreateTagFilters(cards));
    }

    internal static IReadOnlyList<CardEmptyStateRow> CreateEmptyStates(
        int totalCards,
        int visibleCards,
        FlashcardCardsFilterKind filterKind,
        bool isDeckScopeSelected = false,
        int selectedDeckCount = 0)
    {
        if (totalCards == 0)
        {
            if (isDeckScopeSelected)
            {
                return
                [
                    new CardEmptyStateRow(
                        selectedDeckCount == 1 ? "No cards in this deck" : "No cards in selected decks",
                        "Create cards for the selected deck scope or clear the deck selection to see all cards.")
                ];
            }

            return
            [
                new CardEmptyStateRow(
                    "No cards yet",
                    "Create cards from your notes to start reviewing this collection.")
            ];
        }

        if (visibleCards > 0)
        {
            return [];
        }

        return filterKind switch
        {
            FlashcardCardsFilterKind.Due =>
            [
                new CardEmptyStateRow(
                    "No due cards right now",
                    "Come back when your scheduler brings cards due, or create new cards from your notes.")
            ],
            _ =>
            [
                new CardEmptyStateRow(
                    "No cards match this filter",
                    "Try clearing filters or choosing a broader deck, source, or tag.")
            ],
        };
    }

    internal static string CreateActiveFilterSummary(
        CardFilterDeckRow? selectedDeckFilter,
        CardFilterOptionRow? selectedStatusFilter,
        CardFilterOptionRow? selectedSourceFilter,
        FlashcardCardsFilterKind filterKind,
        string? selectedSourceRelPath,
        IReadOnlyList<string> selectedTags)
    {
        List<string> filters = [];

        if (selectedDeckFilter is not null && selectedDeckFilter.Id.HasValue)
        {
            filters.Add(selectedDeckFilter.Name);
        }

        if (filterKind != FlashcardCardsFilterKind.All && selectedStatusFilter is not null)
        {
            filters.Add(selectedStatusFilter.Name);
        }

        if (!string.IsNullOrWhiteSpace(selectedSourceRelPath) && selectedSourceFilter is not null)
        {
            filters.Add($"Source: {selectedSourceFilter.Name}");
        }

        if (selectedTags.Count > 0)
        {
            filters.Add($"Tags: {string.Join(", ", selectedTags)}");
        }

        return filters.Count == 0 ? "All cards" : string.Join(" | ", filters);
    }

    private static CardFilterOptionRow[] CreateStatusFilters(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        DateTimeOffset nowUtc)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        return
        [
            new("All cards", NumericTextFormatter.Format(cards.Count), FlashcardCardsFilterKind.All.ToString()),
            new("Due cards", NumericTextFormatter.Format(cards.Count(card => card.DueUtc <= nowUtc && !card.IsSuspended)), FlashcardCardsFilterKind.Due.ToString()),
            new("New cards", NumericTextFormatter.Format(cards.Count(card => card.State == FlashcardCardStateReadModel.New)), FlashcardCardsFilterKind.New.ToString()),
            new("Learning/relearning", NumericTextFormatter.Format(cards.Count(card => card.State is FlashcardCardStateReadModel.Learning or FlashcardCardStateReadModel.Relearning)), FlashcardCardsFilterKind.LearningOrRelearning.ToString()),
            new("Reviewed today", NumericTextFormatter.Format(cards.Count(card => ToLocalDate(card.LastReviewedUtc) == today)), FlashcardCardsFilterKind.ReviewedToday.ToString()),
            new("Suspended", NumericTextFormatter.Format(cards.Count(card => card.IsSuspended)), FlashcardCardsFilterKind.Suspended.ToString())
        ];
    }

    private static CardFilterOptionRow[] CreateSourceFilters(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        return new[] { new CardFilterOptionRow("All Sources", NumericTextFormatter.Format(cards.Count), AllSourcesValue) }
            .Concat(cards
                .Where(card => !string.IsNullOrWhiteSpace(card.SourceRelPath))
                .GroupBy(card => card.SourceRelPath!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CardFilterOptionRow(
                    FormatSource(group.Key, sourceTitlesByRelPath),
                    NumericTextFormatter.Format(group.Count()),
                    group.Key)))
            .ToArray();
    }

    private static CardFilterOptionRow[] CreateTagFilters(IReadOnlyList<FlashcardCardSummaryReadModel> cards)
    {
        return new[] { new CardFilterOptionRow("All Tags", NumericTextFormatter.Format(cards.Count), AllTagsValue) }
            .Concat(cards
                .SelectMany(card => card.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .GroupBy(tag => tag.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CardFilterOptionRow(
                    group.Key,
                    NumericTextFormatter.Format(group.Count()),
                    group.Key)))
            .ToArray();
    }

    private static string FormatSource(
        string? sourceRelPath,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        if (string.IsNullOrWhiteSpace(sourceRelPath))
        {
            return "No source";
        }

        string normalized = sourceRelPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "No source";
        }

        if (sourceTitlesByRelPath.TryGetValue(normalized, out string? title)
            && !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        string fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return normalized;
        }

        string extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    private static DateOnly? ToLocalDate(DateTimeOffset? value)
    {
        return value.HasValue ? ToLocalDate(value.Value) : null;
    }

    private static DateOnly ToLocalDate(DateTimeOffset value)
    {
        return DateOnly.FromDateTime(value.LocalDateTime);
    }
}
