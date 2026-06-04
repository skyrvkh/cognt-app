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

        CardsSubviewLayoutState layout = CardsSubviewLayoutPolicy.Create(lastLayoutWidth);

        LayoutRoot.Margin = layout.LayoutRootMargin;
        SummaryMetricsGrid.Columns = layout.SummaryMetricColumns;

        ResponsiveLayout.ApplyWrappingRow(
            HeaderActionsPanel,
            layout.UseCompactHeader,
            new Thickness(0, 14, 0, 0),
            ResponsiveLayout.ActionInlineMargin);

        ResponsiveLayout.ApplyWrappingRow(
            FilteredSummaryText,
            layout.UseStackedFilterSummary,
            new Thickness(0, 6, 0, 0),
            ResponsiveLayout.NoMargin);

    }

    private void CardsDataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row
            || row.DataContext is not CardLibraryRow card)
        {
            return;
        }

        CardsSubviewSelection.SelectContextRowForRightClick(CardsDataGrid, row, card);
    }

    private void CardsDataGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row
            || row.DataContext is not CardLibraryRow card)
        {
            return;
        }

        CardsSubviewSelection.SelectSingleRowForPlainLeftClick(CardsDataGrid, row, card);
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
            return;
        }

    }

    private async void PostponePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card
            || !int.TryParse((sender as FrameworkElement)?.Tag?.ToString(), out int days))
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextSchedulableCards(CardsDataGrid, card);
        await PostponeCardsAsync(cards, days);
    }

    private async void ToggleSuspensionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextSchedulableCards(CardsDataGrid, card);
        bool isSuspended = !AreAllCardsSuspended(cards);
        await SetCardsSuspensionAsync(cards, isSuspended);
    }

    private async void CustomPostponeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextSchedulableCards(CardsDataGrid, card);
        PostponeCardDialog dialog = new(
            CreateDueDateArray(cards))
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
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        await EditCardAsync(card);
    }

    private async void EditTagsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextCards(CardsDataGrid, card);
        await AddTagsToCardsAsync(cards);
    }

    private async void AddTagsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextCards(CardsDataGrid, card);
        await AddTagsToCardsAsync(cards);
    }

    private async void DeleteCardsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CardsSubviewSelection.ResolveContextCard(sender) is not CardLibraryRow card)
        {
            return;
        }

        CardLibraryRow[] cards = CardsSubviewSelection.GetContextCards(CardsDataGrid, card);
        await DeleteCardsAsync(cards);
    }

    private async Task PostponeCardsAsync(IReadOnlyList<CardLibraryRow> cards, int days)
    {
        if (ViewModel is null || cards.Count == 0)
        {
            return;
        }

        await ViewModel.PostponeCardsAsync(cards, days);
    }

    private async Task SetCardsSuspensionAsync(IReadOnlyList<CardLibraryRow> cards, bool isSuspended)
    {
        if (ViewModel is null || cards.Count == 0)
        {
            return;
        }

        await ViewModel.SetCardsSuspensionAsync(cards, isSuspended);
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
        string[] assignedTags = CreateAssignedTagsArray(cards);
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

    private static DateTimeOffset[] CreateDueDateArray(IReadOnlyList<CardLibraryRow> cards)
    {
        DateTimeOffset[] dueDates = new DateTimeOffset[cards.Count];
        for (int index = 0; index < cards.Count; index++)
        {
            dueDates[index] = cards[index].DueUtc;
        }

        return dueDates;
    }

    private static bool AreAllCardsSuspended(IReadOnlyList<CardLibraryRow> cards)
    {
        if (cards.Count == 0)
        {
            return false;
        }

        foreach (CardLibraryRow card in cards)
        {
            if (!card.IsSuspended)
            {
                return false;
            }
        }

        return true;
    }

    private static string[] CreateAssignedTagsArray(IReadOnlyList<CardLibraryRow> cards)
    {
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> assignedTags = [];
        foreach (CardLibraryRow card in cards)
        {
            foreach (string tag in card.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && seenTags.Add(tag))
                {
                    assignedTags.Add(tag);
                }
            }
        }

        assignedTags.Sort(StringComparer.OrdinalIgnoreCase);
        return assignedTags.ToArray();
    }
}
