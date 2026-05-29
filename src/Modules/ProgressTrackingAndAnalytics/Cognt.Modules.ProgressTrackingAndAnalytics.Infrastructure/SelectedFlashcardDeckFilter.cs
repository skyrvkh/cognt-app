using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SelectedFlashcardDeckFilter
{
    internal static IReadOnlyList<FlashcardDeckSummaryReadModel> Apply(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        IReadOnlyList<string> selectedDeckNames)
    {
        HashSet<string> selectedDeckNameSet = selectedDeckNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedDeckNameSet.Count == 0)
        {
            return decks;
        }

        List<FlashcardDeckSummaryReadModel> filteredDecks = [];
        foreach (FlashcardDeckSummaryReadModel deck in decks)
        {
            if (selectedDeckNameSet.Contains(deck.Name))
            {
                filteredDecks.Add(deck);
            }
        }

        return filteredDecks;
    }
}
