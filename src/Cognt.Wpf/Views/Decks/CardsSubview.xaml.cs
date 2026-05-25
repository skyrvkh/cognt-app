using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.Views;

namespace CogntApp.Views.Decks;

public partial class CardsSubview : UserControl
{
    private const double CompactHeaderBreakpoint = 900;
    private const double StackedTextBreakpoint = 760;
    private double lastLayoutWidth = double.NaN;

    public CardsSubview()
    {
        InitializeComponent();
    }

    private CardsSubviewViewModel? ViewModel => DataContext as CardsSubviewViewModel;

    private void CardsSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void CardsSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        double width = lastLayoutWidth;
        bool compactHeader = width < CompactHeaderBreakpoint;
        bool stackedText = width < StackedTextBreakpoint;

        LayoutRoot.Margin = GetLayoutRootMargin(width);
        SummaryMetricsGrid.Columns = GetSummaryMetricsColumnCount(width);

        ResponsiveLayout.ApplyWrappingRow(
            HeaderActionsPanel,
            compactHeader,
            new Thickness(0, 14, 0, 0),
            ResponsiveLayout.ActionInlineMargin);

        ResponsiveLayout.ApplyWrappingRow(
            FilteredSummaryText,
            stackedText,
            new Thickness(0, 6, 0, 0),
            ResponsiveLayout.NoMargin);

    }

    private static Thickness GetLayoutRootMargin(double width)
    {
        return width switch
        {
            < 700 => new Thickness(12, 12, 12, 18),
            < 980 => new Thickness(14, 14, 14, 20),
            _ => new Thickness(16, 16, 16, 22),
        };
    }

    private static int GetSummaryMetricsColumnCount(double width)
    {
        return width switch
        {
            < 560 => 1,
            < 820 => 2,
            < 1100 => 3,
            _ => 4,
        };
    }

    private void CardsDataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row
            || row.DataContext is not CardLibraryRow card)
        {
            return;
        }

        if (!row.IsSelected)
        {
            CardsDataGrid.SelectedItems.Clear();
            row.IsSelected = true;
            CardsDataGrid.SelectedItem = card;
        }

        row.Focus();
        row.ContextMenu!.PlacementTarget = row;
    }

    private void CardFilterOptionRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        object? dataContext = (sender as FrameworkElement)?.DataContext;
        if (dataContext is CardFilterOptionRow optionRow)
        {
            optionRow.IsSelected = !optionRow.IsSelected;
            e.Handled = true;
            return;
        }

        if (dataContext is CardFilterDeckRow deckRow)
        {
            deckRow.IsSelected = !deckRow.IsSelected;
            e.Handled = true;
        }
    }

    private async void PostponePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card
            || !int.TryParse((sender as FrameworkElement)?.Tag?.ToString(), out int days))
        {
            return;
        }

        await PostponeCardsAsync(GetContextSchedulableCards(card), days);
    }

    private async void CustomPostponeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = GetContextSchedulableCards(card);
        PostponeCardDialog dialog = new(cards.Select(item => item.DueUtc).ToArray())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            await PostponeCardsAsync(cards, dialog.Days);
        }
    }

    private async void EditCardMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card)
        {
            return;
        }

        await EditCardAsync(card);
    }

    private async void EditTagsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card)
        {
            return;
        }

        await AddTagsToCardsAsync(GetContextCards(card));
    }

    private async void AddTagsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card)
        {
            return;
        }

        await AddTagsToCardsAsync(GetContextCards(card));
    }

    private async void DeleteCardsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CardLibraryRow card)
        {
            return;
        }

        await DeleteCardsAsync(GetContextCards(card));
    }

    private async void AddTagsButton_Click(object sender, RoutedEventArgs e)
    {
        await AddTagsToCardsAsync(GetSelectedCards());
    }

    private async void DeleteCardsButton_Click(object sender, RoutedEventArgs e)
    {
        await DeleteCardsAsync(GetSelectedCards());
    }

    private async Task PostponeCardsAsync(IReadOnlyList<CardLibraryRow> cards, int days)
    {
        if (ViewModel is null || cards.Count == 0)
        {
            return;
        }

        await ViewModel.PostponeCardsAsync(cards, days);
    }

    private async Task EditCardAsync(CardLibraryRow card)
    {
        if (ViewModel is null || !card.CanEdit)
        {
            return;
        }

        EditCardDialog dialog = new(ViewModel.CreateEditorView, card, ViewModel.GetDeckNames())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.EditResult is not null)
        {
            await ViewModel.UpdateCardContentAsync(card, dialog.EditResult);
        }
    }

    private async Task AddTagsToCardsAsync(IReadOnlyList<CardLibraryRow> cards)
    {
        if (ViewModel is null || cards.Count == 0)
        {
            return;
        }

        string title = cards.Count == 1 ? "Selected card" : $"{cards.Count} selected cards";
        string[] assignedTags = cards
            .SelectMany(card => card.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TagEditDialog dialog = new(
            title,
            KnowledgeTagOperationKind.Add,
            ViewModel.GetAvailableTagNames(),
            assignedTags,
            showScope: false)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedTags.Count > 0)
            {
                await ViewModel.AddTagsToCardsAsync(cards, dialog.SelectedTags);
            }

            if (dialog.RemovedTags.Count > 0)
            {
                await ViewModel.RemoveTagsFromCardsAsync(cards, dialog.RemovedTags);
            }
        }
    }

    private async Task DeleteCardsAsync(IReadOnlyList<CardLibraryRow> cards)
    {
        if (ViewModel is null || cards.Count == 0)
        {
            return;
        }

        string message = cards.Count == 1
            ? "Delete the selected card? This removes it from review queues and card lists."
            : $"Delete {cards.Count} selected cards? This removes them from review queues and card lists.";
        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            message,
            "Delete cards",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteCardsAsync(cards);
        }
    }

    private CardLibraryRow[] GetContextSchedulableCards(CardLibraryRow contextCard)
    {
        return GetContextCards(contextCard, requireSchedulable: true);
    }

    private CardLibraryRow[] GetContextCards(CardLibraryRow contextCard)
    {
        return GetContextCards(contextCard, requireSchedulable: false);
    }

    private CardLibraryRow[] GetContextCards(CardLibraryRow contextCard, bool requireSchedulable)
    {
        CardLibraryRow[] selectedCards = CardsDataGrid.SelectedItems
            .OfType<CardLibraryRow>()
            .Where(card => card.Id > 0 && (!requireSchedulable || card.CanSchedule))
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();

        return selectedCards.Any(card => card.Id == contextCard.Id)
            ? selectedCards
            : [contextCard];
    }

    private CardLibraryRow[] GetSelectedCards()
    {
        CardLibraryRow[] selectedCards = CardsDataGrid.SelectedItems
            .OfType<CardLibraryRow>()
            .Where(card => card.Id > 0)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();

        if (selectedCards.Length > 0)
        {
            return selectedCards;
        }

        return CardsDataGrid.SelectedItem is CardLibraryRow selectedCard && selectedCard.Id > 0
            ? [selectedCard]
            : [];
    }
}
