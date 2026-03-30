using System.Windows;
using System.Windows.Controls;
using CogntApp.Views;

namespace CogntApp.Views.Decks;

public partial class CardsSubview : UserControl
{
    private const double PreviewStackBreakpoint = 1180;
    private const double CompactHeaderBreakpoint = 900;
    private const double StackedTextBreakpoint = 760;
    private double lastLayoutWidth = double.NaN;

    public CardsSubview()
    {
        InitializeComponent();
    }

    private void CardsSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void CardsSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void OpenFilters_Click(object sender, RoutedEventArgs e)
    {
        SetFiltersModalVisibility(Visibility.Visible);
    }

    private void CloseFilters_Click(object sender, RoutedEventArgs e)
    {
        SetFiltersModalVisibility(Visibility.Collapsed);
    }

    private void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        SetFiltersModalVisibility(Visibility.Collapsed);
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        double width = lastLayoutWidth;
        bool stackedPreview = width < PreviewStackBreakpoint;
        bool compactHeader = width < CompactHeaderBreakpoint;
        bool stackedText = width < StackedTextBreakpoint;

        LayoutRoot.Margin = GetLayoutRootMargin(width);
        SummaryMetricsGrid.Columns = GetSummaryMetricsColumnCount(width);

        LayoutGapColumnDefinition.Width = stackedPreview
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(14);
        PreviewColumnDefinition.Width = stackedPreview
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(276);

        ResponsiveLayout.Place(
            PreviewPanel,
            stackedPreview ? 1 : 0,
            stackedPreview ? 0 : 2,
            stackedPreview ? 3 : 1);
        PreviewPanel.Margin = stackedPreview ? new Thickness(0, 16, 0, 0) : new Thickness(0);
        PreviewPanel.Height = stackedPreview ? double.NaN : 660;

        ResponsiveLayout.ApplyWrappingRow(
            HeaderActionsPanel,
            compactHeader,
            new Thickness(0, 14, 0, 0),
            new Thickness(12, 0, 0, 0));

        ResponsiveLayout.ApplyWrappingRow(
            FilteredSummaryText,
            stackedText,
            new Thickness(0, 6, 0, 0),
            ResponsiveLayout.NoMargin);

        ResponsiveLayout.ApplyWrappingRow(
            PaginationButtonsPanel,
            compactHeader,
            new Thickness(0, 10, 0, 0),
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
            _ => 5,
        };
    }

    private void SetFiltersModalVisibility(Visibility visibility)
    {
        FiltersModalOverlay.Visibility = visibility;
    }
}
