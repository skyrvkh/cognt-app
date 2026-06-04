using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Views.Decks;

internal static class CardsSubviewSelection
{
    internal static void SelectContextRowForRightClick(
        DataGrid cardsGrid,
        DataGridRow row,
        CardLibraryRow card)
    {
        if (!row.IsSelected)
        {
            cardsGrid.SelectedItems.Clear();
            row.IsSelected = true;
            cardsGrid.SelectedItem = card;
        }

        row.Focus();
        row.ContextMenu!.PlacementTarget = row;
    }

    internal static void SelectSingleRowForPlainLeftClick(
        DataGrid cardsGrid,
        DataGridRow row,
        CardLibraryRow card)
    {
        ModifierKeys rangeOrToggleModifiers = ModifierKeys.Control | ModifierKeys.Shift;
        if ((Keyboard.Modifiers & rangeOrToggleModifiers) != ModifierKeys.None)
        {
            return;
        }

        if (cardsGrid.SelectedItems.Count != 1 || !row.IsSelected)
        {
            cardsGrid.SelectedItems.Clear();
            row.IsSelected = true;
            cardsGrid.SelectedItem = card;
        }

        row.Focus();
    }

    internal static CardLibraryRow[] GetContextSchedulableCards(
        DataGrid cardsGrid,
        CardLibraryRow contextCard)
    {
        return GetContextCards(cardsGrid, contextCard, requireSchedulable: true);
    }

    internal static CardLibraryRow[] GetContextCards(
        DataGrid cardsGrid,
        CardLibraryRow contextCard)
    {
        return GetContextCards(cardsGrid, contextCard, requireSchedulable: false);
    }

    internal static CardLibraryRow? ResolveContextCard(object sender)
    {
        if (sender is FrameworkElement { DataContext: CardLibraryRow card })
        {
            return card;
        }

        if (sender is FrameworkElement { Parent: ContextMenu contextMenu }
            && contextMenu.PlacementTarget is FrameworkElement { DataContext: CardLibraryRow placementCard })
        {
            return placementCard;
        }

        return null;
    }

    private static CardLibraryRow[] GetContextCards(
        DataGrid cardsGrid,
        CardLibraryRow contextCard,
        bool requireSchedulable)
    {
        CardLibraryRow[] selectedCards = GetSelectedGridCards(cardsGrid, requireSchedulable);

        return ContainsCard(selectedCards, contextCard.Id)
            ? selectedCards
            : [contextCard];
    }

    private static CardLibraryRow[] GetSelectedGridCards(DataGrid cardsGrid, bool requireSchedulable)
    {
        if (cardsGrid.SelectedItems.Count == 0)
        {
            return [];
        }

        List<CardLibraryRow>? selectedCards = null;
        foreach (object? item in cardsGrid.SelectedItems)
        {
            if (item is not CardLibraryRow card)
            {
                continue;
            }

            selectedCards ??= new List<CardLibraryRow>(cardsGrid.SelectedItems.Count);
            selectedCards.Add(card);
        }

        if (selectedCards is null)
        {
            return [];
        }

        return requireSchedulable
            ? CardLibraryRowSelection.DistinctSchedulable(selectedCards)
            : CardLibraryRowSelection.DistinctPersisted(selectedCards);
    }

    private static bool ContainsCard(IReadOnlyList<CardLibraryRow> cards, long cardId)
    {
        foreach (CardLibraryRow card in cards)
        {
            if (card.Id == cardId)
            {
                return true;
            }
        }

        return false;
    }
}
