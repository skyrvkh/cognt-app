using System.Windows;
using System.Windows.Controls;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.Views;

namespace CogntApp.Views.Decks;

public partial class DecksSubview : UserControl
{
    private const double StackedToolbarBreakpoint = 1100;
    private double lastLayoutWidth = double.NaN;

    public DecksSubview()
    {
        InitializeComponent();
    }

    private DecksSubviewViewModel? ViewModel => DataContext as DecksSubviewViewModel;

    private void DecksSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void DecksSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private async void DeleteSelectedDecksButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        IReadOnlyList<DeckRow> selectedDecks = ViewModel.SelectedDeckRows;
        if (selectedDecks.Count == 0)
        {
            return;
        }

        string message = selectedDecks.Count == 1
            ? $"Delete deck \"{selectedDecks[0].Name}\"? Cards in it will move to Default."
            : $"Delete {selectedDecks.Count} selected decks? Cards in them will move to Default.";
        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            message,
            "Delete decks",
            MessageBoxButton.YesNo,
        MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteSelectedDecksAsync();
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        bool stackedToolbars = lastLayoutWidth < StackedToolbarBreakpoint;

        ApplyToolbarLayout(DecksToolbarActionsPanel, DecksToolbarSpacerRow, stackedToolbars);
    }

    private static void ApplyToolbarLayout(FrameworkElement actionPanel, RowDefinition spacerRow, bool stackedToolbar)
    {
        ResponsiveLayout.ApplyWrappingRow(
            actionPanel,
            spacerRow,
            stackedToolbar,
            ResponsiveLayout.ActionWrappedMargin,
            ResponsiveLayout.NoMargin);
    }
}
