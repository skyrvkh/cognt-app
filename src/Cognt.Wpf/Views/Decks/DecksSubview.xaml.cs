using System.Windows;
using System.Windows.Controls;
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
