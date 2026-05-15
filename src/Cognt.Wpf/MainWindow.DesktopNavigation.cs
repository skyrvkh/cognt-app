using System.Windows;

namespace CogntApp;

public partial class MainWindow
{
    private const double DefaultSidebarWidth = 264.0d;

    private void ApplyDesktopNavigation()
    {
        SidebarColumn.MinWidth = 240.0d;
        SidebarColumn.MaxWidth = 320.0d;
        SidebarColumn.Width = new GridLength(DefaultSidebarWidth);

        SidebarHost.Visibility = Visibility.Visible;
        SidebarHost.IsEnabled = true;
        SidebarHost.IsHitTestVisible = true;
        SidebarHost.CornerRadius = new CornerRadius(0);
        SidebarShadowEffect.Opacity = 0;
        SidebarHeader.Margin = new Thickness(0, 0, 0, 12);

        SidebarBrandText.Visibility = Visibility.Visible;
        SidebarTopNavigationSection.Visibility = Visibility.Visible;
        SidebarScrollContent.Visibility = Visibility.Visible;
        SidebarFooterSection.Visibility = Visibility.Visible;
    }
}
