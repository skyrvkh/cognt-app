using System.Windows;

namespace CogntApp.Views.Decks;

internal readonly record struct CardsSubviewLayoutState(
    Thickness LayoutRootMargin,
    int SummaryMetricColumns,
    bool UseCompactHeader,
    bool UseStackedFilterSummary);

internal static class CardsSubviewLayoutPolicy
{
    private const double CompactHeaderBreakpoint = 900;
    private const double StackedTextBreakpoint = 760;

    internal static CardsSubviewLayoutState Create(double width)
    {
        return new CardsSubviewLayoutState(
            GetLayoutRootMargin(width),
            GetSummaryMetricsColumnCount(width),
            UseCompactHeader: width < CompactHeaderBreakpoint,
            UseStackedFilterSummary: width < StackedTextBreakpoint);
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
}
