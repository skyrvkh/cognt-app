using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class CardsPresentationFactory
{
    private static readonly IReadOnlyDictionary<string, string> EmptySourceTitlesByRelPath =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal static CardsFilterOptionsState CreateFilterOptions(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, string>? sourceTitlesByRelPath = null)
    {
        DeckFilterState deckState = CreateDeckFilterState(decks);
        CardFilterCounts counts = CreateCardFilterCounts(
            cards,
            nowUtc,
            DateOnly.FromDateTime(DateTime.Today));

        return new CardsFilterOptionsState(
            deckState.Rows,
            CreateStatusFilters(cards.Count, counts.Status),
            CreateSourceFilters(cards.Count, counts.SourceCounts, sourceTitlesByRelPath ?? EmptySourceTitlesByRelPath),
            CreateTagFilters(cards.Count, counts.TagCounts),
            deckState.DeckNames,
            deckState.TotalCardCount,
            counts.Summary);
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
        IReadOnlyList<string> selectedTags,
        string? searchText)
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

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filters.Add($"Search: {searchText.Trim()}");
        }

        return filters.Count == 0 ? "All cards" : string.Join(" | ", filters);
    }

    private static CardFilterOptionRow[] CreateStatusFilters(
        int totalCards,
        CardStatusFilterCounts counts)
    {
        return
        [
            new("All cards", NumericTextFormatter.Format(totalCards), FlashcardCardsFilterKind.All.ToString()),
            new("Due cards", NumericTextFormatter.Format(counts.DueCards), FlashcardCardsFilterKind.Due.ToString()),
            new("New cards", NumericTextFormatter.Format(counts.NewCards), FlashcardCardsFilterKind.New.ToString()),
            new("Learning/relearning", NumericTextFormatter.Format(counts.LearningOrRelearningCards), FlashcardCardsFilterKind.LearningOrRelearning.ToString()),
            new("Reviewed today", NumericTextFormatter.Format(counts.ReviewedTodayCards), FlashcardCardsFilterKind.ReviewedToday.ToString()),
            new("Suspended", NumericTextFormatter.Format(counts.SuspendedCards), FlashcardCardsFilterKind.Suspended.ToString())
        ];
    }

    private static CardFilterCounts CreateCardFilterCounts(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        DateTimeOffset nowUtc,
        DateOnly today)
    {
        int dueCards = 0;
        int summaryDueCards = 0;
        int newCards = 0;
        int summaryLearningCards = 0;
        int learningOrRelearningCards = 0;
        int summaryRelearningCards = 0;
        int reviewedTodayCards = 0;
        int suspendedCards = 0;
        Dictionary<string, int> sourceCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> tagCounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (FlashcardCardSummaryReadModel card in cards)
        {
            if (card.DueUtc <= nowUtc)
            {
                summaryDueCards++;
                if (!card.IsSuspended)
                {
                    dueCards++;
                }
            }

            if (card.State == FlashcardCardStateReadModel.New)
            {
                newCards++;
                summaryLearningCards++;
            }

            if (card.State == FlashcardCardStateReadModel.Learning)
            {
                summaryLearningCards++;
                learningOrRelearningCards++;
            }

            if (card.State == FlashcardCardStateReadModel.Relearning)
            {
                summaryRelearningCards++;
                learningOrRelearningCards++;
            }

            if (ToLocalDate(card.LastReviewedUtc) == today)
            {
                reviewedTodayCards++;
            }

            if (card.IsSuspended)
            {
                suspendedCards++;
            }

            CountSource(card, sourceCounts);
            CountTags(card, tagCounts);
        }

        return new CardFilterCounts(
            new CardStatusFilterCounts(
                dueCards,
                newCards,
                learningOrRelearningCards,
                reviewedTodayCards,
                suspendedCards),
            new CardsSummaryCounts(
                summaryLearningCards,
                summaryRelearningCards,
                summaryDueCards),
            sourceCounts,
            tagCounts);
    }

    private readonly record struct CardFilterCounts(
        CardStatusFilterCounts Status,
        CardsSummaryCounts Summary,
        Dictionary<string, int> SourceCounts,
        Dictionary<string, int> TagCounts);

    private readonly record struct CardStatusFilterCounts(
        int DueCards,
        int NewCards,
        int LearningOrRelearningCards,
        int ReviewedTodayCards,
        int SuspendedCards);

    private static DeckFilterState CreateDeckFilterState(IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        int totalCardCount = 0;
        CardFilterDeckRow[] rows = new CardFilterDeckRow[decks.Count + 1];
        HashSet<string> seenDeckNames = new(StringComparer.OrdinalIgnoreCase);
        List<string> deckNames = new(decks.Count);

        for (int index = 0; index < decks.Count; index++)
        {
            FlashcardDeckSummaryReadModel deck = decks[index];
            totalCardCount += deck.TotalCards;
            rows[index + 1] = new CardFilterDeckRow(
                deck.Name,
                NumericTextFormatter.Format(deck.TotalCards),
                deck.Id);

            string name = deck.Name;
            if (!string.IsNullOrWhiteSpace(name) && seenDeckNames.Add(name))
            {
                deckNames.Add(name);
            }
        }

        deckNames.Sort(StringComparer.OrdinalIgnoreCase);
        rows[0] = new CardFilterDeckRow("All Decks", NumericTextFormatter.Format(totalCardCount));

        return new DeckFilterState(
            rows,
            deckNames.ToArray(),
            totalCardCount);
    }

    private readonly record struct DeckFilterState(
        CardFilterDeckRow[] Rows,
        string[] DeckNames,
        int TotalCardCount);

    private static CardFilterOptionRow[] CreateSourceFilters(
        int totalCards,
        Dictionary<string, int> sourceCounts,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        CardFilterOptionRow[] sourceRows = CreateSortedSourceRows(sourceCounts, sourceTitlesByRelPath);

        return CreateOptionRows("All Sources", AllSourcesValue, totalCards, sourceRows);
    }

    private static CardFilterOptionRow[] CreateTagFilters(
        int totalCards,
        Dictionary<string, int> tagCounts)
    {
        CardFilterOptionRow[] tagRows = CreateSortedTagRows(tagCounts);

        return CreateOptionRows("All Tags", AllTagsValue, totalCards, tagRows);
    }

    private static void CountSource(
        FlashcardCardSummaryReadModel card,
        Dictionary<string, int> sourceCounts)
    {
        if (string.IsNullOrWhiteSpace(card.SourceRelPath))
        {
            return;
        }

        string sourceRelPath = card.SourceRelPath!;
        sourceCounts[sourceRelPath] = sourceCounts.TryGetValue(sourceRelPath, out int count)
            ? count + 1
            : 1;
    }

    private static void CountTags(
        FlashcardCardSummaryReadModel card,
        Dictionary<string, int> tagCounts)
    {
        foreach (string tag in card.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            string normalizedTag = tag.Trim();
            if (normalizedTag.StartsWith("source:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tagCounts[normalizedTag] = tagCounts.TryGetValue(normalizedTag, out int count)
                ? count + 1
                : 1;
        }
    }

    private static CardFilterOptionRow[] CreateSortedSourceRows(
        Dictionary<string, int> sourceCounts,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath)
    {
        List<KeyValuePair<string, int>> sources = new(sourceCounts);
        sources.Sort(CompareCountEntriesByKey);

        CardFilterOptionRow[] rows = new CardFilterOptionRow[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            KeyValuePair<string, int> source = sources[index];
            rows[index] = new CardFilterOptionRow(
                FormatSource(source.Key, sourceTitlesByRelPath),
                NumericTextFormatter.Format(source.Value),
                source.Key);
        }

        return rows;
    }

    private static CardFilterOptionRow[] CreateSortedTagRows(Dictionary<string, int> tagCounts)
    {
        List<KeyValuePair<string, int>> tags = new(tagCounts);
        tags.Sort(CompareCountEntriesByKey);

        CardFilterOptionRow[] rows = new CardFilterOptionRow[tags.Count];
        for (int index = 0; index < tags.Count; index++)
        {
            KeyValuePair<string, int> tag = tags[index];
            rows[index] = new CardFilterOptionRow(
                tag.Key,
                NumericTextFormatter.Format(tag.Value),
                tag.Key);
        }

        return rows;
    }

    private static int CompareCountEntriesByKey(
        KeyValuePair<string, int> left,
        KeyValuePair<string, int> right)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key);
    }

    private static CardFilterOptionRow[] CreateOptionRows(
        string allLabel,
        string allValue,
        int totalCards,
        IReadOnlyList<CardFilterOptionRow> optionRows)
    {
        CardFilterOptionRow[] rows = new CardFilterOptionRow[optionRows.Count + 1];
        rows[0] = new CardFilterOptionRow(allLabel, NumericTextFormatter.Format(totalCards), allValue);

        for (int index = 0; index < optionRows.Count; index++)
        {
            rows[index + 1] = optionRows[index];
        }

        return rows;
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


