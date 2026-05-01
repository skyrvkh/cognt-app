using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private const double DefaultSidebarWidth = 300;
    private const double MinSidebarWidth = 220;
    private const double MaxSidebarWidth = 420;
    private static readonly Geometry ChevronLeftIcon = CreateFrozenGeometry("M11.5,4 L5.5,10 L11.5,16");

    private bool isDesktopSidebarCollapsed;
    private double desktopSidebarExpandedWidth = DefaultSidebarWidth;

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        isDesktopSidebarCollapsed = !isDesktopSidebarCollapsed;
        ApplyDesktopNavigation();
    }

    private void FloatingNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        isDesktopSidebarCollapsed = false;
        ApplyDesktopNavigation();
    }

    private void SidebarSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (isDesktopSidebarCollapsed)
        {
            return;
        }

        desktopSidebarExpandedWidth = ClampSidebarWidth(SidebarColumn.ActualWidth);
        SidebarColumn.Width = ResponsiveLayout.Pixel(desktopSidebarExpandedWidth);
    }

    private void SidebarSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (isDesktopSidebarCollapsed)
        {
            return;
        }

        double currentWidth = SidebarColumn.ActualWidth > 0
            ? SidebarColumn.ActualWidth
            : desktopSidebarExpandedWidth;
        desktopSidebarExpandedWidth = ClampSidebarWidth(currentWidth + e.HorizontalChange);
        SidebarColumn.Width = ResponsiveLayout.Pixel(desktopSidebarExpandedWidth);
    }

    private void ApplyDesktopNavigation()
    {
        bool desktopNavigationHidden = isDesktopSidebarCollapsed;
        bool showExpandedSidebarContent = !desktopNavigationHidden;

        if (SidebarColumn.ActualWidth >= MinSidebarWidth)
        {
            desktopSidebarExpandedWidth = ClampSidebarWidth(SidebarColumn.ActualWidth);
        }

        SidebarColumn.MinWidth = desktopNavigationHidden ? 0 : MinSidebarWidth;
        SidebarColumn.MaxWidth = MaxSidebarWidth;
        SidebarColumn.Width = desktopNavigationHidden
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(ClampSidebarWidth(desktopSidebarExpandedWidth));
        SidebarSplitterColumn.Width = desktopNavigationHidden
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(6);
        SidebarSplitter.Visibility = ResponsiveLayout.VisibleWhen(!desktopNavigationHidden);
        SidebarSplitter.IsHitTestVisible = !desktopNavigationHidden;
        SidebarHost.Visibility = ResponsiveLayout.VisibleWhen(!desktopNavigationHidden);
        SidebarHost.IsEnabled = !desktopNavigationHidden;
        SidebarHost.IsHitTestVisible = !desktopNavigationHidden;
        FloatingNavigationButton.Visibility = ResponsiveLayout.VisibleWhen(desktopNavigationHidden);
        FloatingNavigationButton.IsHitTestVisible = desktopNavigationHidden;
        SidebarHost.CornerRadius = new CornerRadius(0);
        SidebarShadowEffect.Opacity = 0;
        SidebarHeader.Margin = showExpandedSidebarContent
            ? new Thickness(0, 0, 0, 12)
            : new Thickness(0);
        Visibility expandedSidebarVisibility = ResponsiveLayout.VisibleWhen(showExpandedSidebarContent);
        SidebarBrandText.Visibility = expandedSidebarVisibility;
        SidebarTopNavigationSection.Visibility = expandedSidebarVisibility;
        SidebarScrollContent.Visibility = expandedSidebarVisibility;
        SidebarFooterSection.Visibility = expandedSidebarVisibility;

        SidebarToggleIcon.Data = ChevronLeftIcon;
        SidebarToggleButton.ToolTip = "Hide navigation";
    }

    private static double ClampSidebarWidth(double width)
    {
        return Math.Clamp(width, MinSidebarWidth, MaxSidebarWidth);
    }

    private static Geometry CreateFrozenGeometry(string pathData)
    {
        Geometry geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
