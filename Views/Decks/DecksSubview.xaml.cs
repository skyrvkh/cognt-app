using System.Windows;
using System.Windows.Controls;
using CogntApp.Views;

namespace CogntApp.Views.Decks;

public partial class DecksSubview : UserControl
{
    private const double StackedLayoutBreakpoint = 1180;
    private const double StackedToolbarBreakpoint = 1100;
    private const double StackedPaginationBreakpoint = 950;
    private double lastLayoutWidth = double.NaN;

    public DecksSubview()
    {
        InitializeComponent();
    }

    private void DecksSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void DecksSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        bool stackedLayout = lastLayoutWidth < StackedLayoutBreakpoint;
        bool stackedToolbars = lastLayoutWidth < StackedToolbarBreakpoint;
        bool stackedPagination = lastLayoutWidth < StackedPaginationBreakpoint;

        ApplyRootLayout(stackedLayout);
        ApplyToolbarLayout(DecksToolbarActionsPanel, DecksToolbarSpacerRow, stackedToolbars);
        ApplyToolbarLayout(CardsToolbarActionsPanel, CardsToolbarSpacerRow, stackedToolbars);
        ApplyPaginationLayout(stackedPagination);
    }

    private void ApplyRootLayout(bool stackedLayout)
    {
        OverviewColumn.Width = stackedLayout
            ? ResponsiveLayout.OneStar
            : ResponsiveLayout.Pixel(260);
        LayoutGapColumn.Width = stackedLayout
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(14);
        ContentColumn.Width = ResponsiveLayout.OneStar;
        LayoutGapRow.Height = stackedLayout
            ? ResponsiveLayout.Pixel(14)
            : ResponsiveLayout.Zero;

        ResponsiveLayout.Place(
            OverviewPanel,
            0,
            0,
            stackedLayout ? 3 : 1,
            stackedLayout ? 1 : 3);
        ResponsiveLayout.Place(
            ContentPanel,
            stackedLayout ? 2 : 0,
            stackedLayout ? 0 : 2,
            stackedLayout ? 3 : 1,
            stackedLayout ? 1 : 3);
    }

    private void ApplyPaginationLayout(bool stackedPagination)
    {
        ResponsiveLayout.ApplyWrappingRow(
            CardsPaginationPanel,
            CardsPaginationSpacerRow,
            stackedPagination,
            new Thickness(0, 10, 0, 0),
            ResponsiveLayout.NoMargin);
    }

    private static void ApplyToolbarLayout(FrameworkElement actionPanel, RowDefinition spacerRow, bool stackedToolbar)
    {
        ResponsiveLayout.ApplyWrappingRow(
            actionPanel,
            spacerRow,
            stackedToolbar,
            new Thickness(0, 12, 0, 0),
            ResponsiveLayout.NoMargin);
    }
}
