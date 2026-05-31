using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class CardSummaryMaterializer
{
    private static string NormalizeSearchText(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();
    }

    private static bool MatchesSearch(
        FlashcardCardSummaryRecord card,
        MaterializedFlashcardContent materialized,
        string searchText)
    {
        if (searchText.Length == 0)
        {
            return true;
        }

        return ContainsSearchText(materialized.FrontText, searchText)
            || ContainsSearchText(materialized.BackText, searchText)
            || ContainsSearchText(card.DeckName, searchText)
            || ContainsSearchText(card.SourceRelPath, searchText)
            || ContainsTagSearchText(materialized.Tags, searchText);
    }

    private static bool ContainsTagSearchText(IReadOnlyList<string> tags, string searchText)
    {
        foreach (string tag in tags)
        {
            if (ContainsSearchText(tag, searchText))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSearchText(string? value, string searchText)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}
