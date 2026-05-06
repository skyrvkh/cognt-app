using System.Windows;
using System.Windows.Controls;
using CogntApp.Views;

namespace CogntApp.Views.Decks;

public partial class StatisticsSubview : UserControl
{
    private const double StackedLayoutBreakpoint = 1100;
    private const double ThreeColumnSummaryBreakpoint = 940;
    private const double TwoColumnSummaryBreakpoint = 620;
    private double lastLayoutWidth = double.NaN;

    public StatisticsSubview()
    {
        InitializeComponent();
    }

    private void StatisticsSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void StatisticsSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        double availableWidth = Math.Max(
            0,
            lastLayoutWidth - StatisticsLayoutRoot.Margin.Left - StatisticsLayoutRoot.Margin.Right);

        StatisticsSummaryGrid.Columns = GetSummaryColumnCount(availableWidth);
        bool useStackedLayout = availableWidth < StackedLayoutBreakpoint;

        ApplyAnalyticsSectionLayout(
            useStackedLayout,
            TopStatisticsGapRow,
            TopStatisticsSecondaryRow,
            TopStatisticsPrimaryColumn,
            TopStatisticsGapColumn,
            TopStatisticsSecondaryColumn,
            TopStatisticsSidePanel);

        ApplyAnalyticsSectionLayout(
            useStackedLayout,
            BottomStatisticsGapRow,
            BottomStatisticsSecondaryRow,
            BottomStatisticsPrimaryColumn,
            BottomStatisticsGapColumn,
            BottomStatisticsSecondaryColumn,
            WeeklyOutcomePanel);

        DailyStatisticsChart.Height = useStackedLayout ? 320 : 360;
        RecallDistributionChart.Height = useStackedLayout ? 200 : 220;
        StudyHeatmapChart.Height = useStackedLayout ? 200 : 220;
        WeeklyOutcomeChart.Height = useStackedLayout ? 220 : 245;
    }

    private static int GetSummaryColumnCount(double availableWidth)
    {
        return availableWidth switch
        {
            >= StackedLayoutBreakpoint => 5,
            >= ThreeColumnSummaryBreakpoint => 3,
            >= TwoColumnSummaryBreakpoint => 2,
            _ => 1,
        };
    }

    private static void ApplyAnalyticsSectionLayout(
        bool useStackedLayout,
        RowDefinition gapRow,
        RowDefinition secondaryRow,
        ColumnDefinition primaryColumn,
        ColumnDefinition gapColumn,
        ColumnDefinition secondaryColumn,
        FrameworkElement secondaryElement)
    {
        if (useStackedLayout)
        {
            gapRow.Height = ResponsiveLayout.Pixel(14);
            secondaryRow.Height = ResponsiveLayout.Auto;
            primaryColumn.Width = ResponsiveLayout.OneStar;
            gapColumn.Width = ResponsiveLayout.Zero;
            secondaryColumn.Width = ResponsiveLayout.Zero;

            ResponsiveLayout.Place(secondaryElement, 2, 0);
            return;
        }

        gapRow.Height = ResponsiveLayout.Zero;
        secondaryRow.Height = ResponsiveLayout.Zero;
        primaryColumn.Width = ResponsiveLayout.Star(1.55);
        gapColumn.Width = ResponsiveLayout.Pixel(14);
        secondaryColumn.Width = ResponsiveLayout.OneStar;

        ResponsiveLayout.Place(secondaryElement, 0, 2);
    }
}
