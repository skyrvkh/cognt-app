using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        row.IsSelected = true;
        row.Focus();
        CardsDataGrid.SelectedItem = card;
        row.ContextMenu!.PlacementTarget = row;
    }

    private void ShowCardPreview(CardLibraryRow card)
    {
        CardPreviewWindow previewWindow = new(card);

        Window? owner = Window.GetWindow(this);
        if (owner is not null)
        {
            previewWindow.Owner = owner;
        }

        previewWindow.ShowDialog();
    }

    private void PreviewCardMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CardLibraryRow card)
        {
            ShowCardPreview(card);
        }
    }
}
