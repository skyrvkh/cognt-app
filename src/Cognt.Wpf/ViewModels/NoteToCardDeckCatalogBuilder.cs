using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

internal sealed record NoteToCardDeckCatalog(
    IReadOnlyList<string> DeckNames,
    string? FirstDeckName);

internal static class NoteToCardDeckCatalogBuilder
{
    internal static NoteToCardDeckCatalog Create(IEnumerable<FlashcardDeckSummaryReadModel> decks)
    {
        var deckNames = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? firstDeckName = null;

        foreach (FlashcardDeckSummaryReadModel deck in decks)
        {
            string name = deck.Name;
            if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
            {
                continue;
            }

            firstDeckName ??= name;
            deckNames.Add(name);
        }

        deckNames.Sort(StringComparer.OrdinalIgnoreCase);
        return new NoteToCardDeckCatalog(deckNames, firstDeckName);
    }

    internal static string ResolveSelection(
        string previousSelection,
        IReadOnlyList<string> deckNames,
        string? firstDeckName)
    {
        if (string.IsNullOrWhiteSpace(previousSelection) && firstDeckName is not null)
        {
            return firstDeckName;
        }

        if (!string.IsNullOrWhiteSpace(previousSelection))
        {
            return previousSelection;
        }

        return deckNames.Count > 0
            ? deckNames[0]
            : string.Empty;
    }

    internal static IReadOnlyList<string> FilterSearchResults(
        IReadOnlyList<string> deckNames,
        IReadOnlySet<string> deckNameSet,
        string query,
        int limit)
    {
        string trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery)
            || deckNameSet.Contains(trimmedQuery))
        {
            return [];
        }

        var results = new List<string>(limit);
        for (int index = 0; index < deckNames.Count && results.Count < limit; index++)
        {
            string deckName = deckNames[index];
            if (deckName.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(deckName);
            }
        }

        return results;
    }
}
