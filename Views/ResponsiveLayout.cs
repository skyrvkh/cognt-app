using System;
using System.Windows;
using System.Windows.Controls;

namespace CogntApp.Views;

internal static class ResponsiveLayout
{
    public static readonly GridLength Zero = new(0);
    public static readonly GridLength Auto = GridLength.Auto;
    public static readonly GridLength OneStar = new(1, GridUnitType.Star);
    public static readonly Thickness NoMargin = new(0);
    public static readonly Thickness ActionInlineMargin = new(12, 0, 0, 0);
    public static readonly Thickness ActionWrappedMargin = new(0, 12, 0, 0);

    public static GridLength Pixel(double value)
    {
        return new GridLength(value);
    }

    public static GridLength Star(double value)
    {
        return new GridLength(value, GridUnitType.Star);
    }

    public static Visibility VisibleWhen(bool isVisible)
    {
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public static bool TryBeginWidthLayout(double width, ref double lastAppliedWidth)
    {
        if (!IsUsableSize(width))
        {
            return false;
        }

        if (width.Equals(lastAppliedWidth))
        {
            return false;
        }

        lastAppliedWidth = width;
        return true;
    }

    public static bool TryBeginSizeLayout(double width, double height, ref Size lastAppliedSize)
    {
        if (!IsUsableSize(width) || !IsUsableSize(height))
        {
            return false;
        }

        Size nextSize = new(width, height);
        if (!lastAppliedSize.IsEmpty && nextSize.Equals(lastAppliedSize))
        {
            return false;
        }

        lastAppliedSize = nextSize;
        return true;
    }

    public static void Place(
        FrameworkElement element,
        int row,
        int column,
        int columnSpan = 1,
        int rowSpan = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
        Grid.SetRowSpan(element, rowSpan);
    }

    public static void ApplyWrappingRow(
        FrameworkElement element,
        RowDefinition spacerRow,
        bool shouldWrap,
        Thickness wrappedMargin,
        Thickness inlineMargin,
        int inlineColumn = 1)
    {
        spacerRow.Height = shouldWrap ? Auto : Zero;
        ApplyWrappingRow(element, shouldWrap, wrappedMargin, inlineMargin, inlineColumn);
    }

    public static void ApplyWrappingRow(
        FrameworkElement element,
        bool shouldWrap,
        Thickness wrappedMargin,
        Thickness inlineMargin,
        int inlineColumn = 1)
    {
        Place(
            element,
            shouldWrap ? 1 : 0,
            shouldWrap ? 0 : inlineColumn,
            shouldWrap ? 2 : 1);

        element.HorizontalAlignment = shouldWrap
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        element.Margin = shouldWrap ? wrappedMargin : inlineMargin;
    }

    private static bool IsUsableSize(double value)
    {
        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
