using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

internal static class CardLibraryRowSelection
{
    internal static CardLibraryRow[] DistinctPersisted(IReadOnlyList<CardLibraryRow> cards)
    {
        return Distinct(cards, static card => card.Id > 0);
    }

    internal static long[] DistinctPersistedIds(IReadOnlyList<CardLibraryRow> cards)
    {
        if (cards.Count == 0)
        {
            return [];
        }

        HashSet<long> seenIds = new();
        List<long>? distinctIds = null;

        foreach (CardLibraryRow card in cards)
        {
            if (card.Id <= 0 || !seenIds.Add(card.Id))
            {
                continue;
            }

            distinctIds ??= new List<long>(cards.Count);
            distinctIds.Add(card.Id);
        }

        return distinctIds is null ? [] : [.. distinctIds];
    }

    internal static CardLibraryRow[] DistinctSchedulable(IReadOnlyList<CardLibraryRow> cards)
    {
        return Distinct(cards, static card => card.CanSchedule);
    }

    private static CardLibraryRow[] Distinct(
        IReadOnlyList<CardLibraryRow> cards,
        Func<CardLibraryRow, bool> predicate)
    {
        if (cards.Count == 0)
        {
            return [];
        }

        HashSet<long> seenIds = new();
        List<CardLibraryRow>? distinctCards = null;

        foreach (CardLibraryRow card in cards)
        {
            if (!predicate(card) || !seenIds.Add(card.Id))
            {
                continue;
            }

            distinctCards ??= new List<CardLibraryRow>(cards.Count);
            distinctCards.Add(card);
        }

        return distinctCards is null ? [] : [.. distinctCards];
    }
}
