using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Views;

public partial class BrowserPaneView : UserControl
{
    private readonly ILogger<BrowserPaneView>? logger;
    private bool isNavigating;

    public BrowserPaneView(ILogger<BrowserPaneView>? logger = null)
    {
        this.logger = logger;
        InitializeComponent();
        RebuildTabStrip();
        UpdateNavigationState();
    }

    internal void FocusAddressBox()
    {
        AddressTextBox.Focus();
        AddressTextBox.SelectAll();
    }

    internal async Task NavigateAsync(string address)
    {
        string? target = NormalizeAddress(address);
        if (target is null)
        {
            ShowState("Browser", "Enter a web address or search term.");
            return;
        }

        AddressTextBox.Text = target;
        ShowState("Loading", target);
        SetBrowserStatus($"Loading {target}");

        try
        {
            isNavigating = true;
            UpdateNavigationState();
            await BrowserWebView.EnsureCoreWebView2Async();
            ConfigureCoreWebView(BrowserWebView.CoreWebView2);
            BrowserWebView.CoreWebView2.Navigate(target);
            BrowserWebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            isNavigating = false;
            logger?.LogError(ex, "Browser navigation failed before dispatch. Target={Target}", target);
            ShowState("Failed to open page", ex.Message);
            SetBrowserStatus(null);
            UpdateNavigationState();
        }
    }

    private static void ConfigureCoreWebView(CoreWebView2 coreWebView)
    {
        coreWebView.Settings.AreDevToolsEnabled = false;
        coreWebView.Settings.IsStatusBarEnabled = true;
        coreWebView.Settings.AreHostObjectsAllowed = false;
        coreWebView.Settings.IsPasswordAutosaveEnabled = false;
        coreWebView.Settings.IsGeneralAutofillEnabled = false;
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        await NavigateAsync(AddressTextBox.Text);
    }

    private async void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await NavigateAsync(AddressTextBox.Text);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoBack == true)
        {
            BrowserWebView.CoreWebView2.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoForward == true)
        {
            BrowserWebView.CoreWebView2.GoForward();
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        BrowserWebView.CoreWebView2?.Reload();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        BrowserWebView.CoreWebView2?.Stop();
        isNavigating = false;
        UpdateNavigationState();
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        AddressTextBox.Text = string.Empty;
        BrowserWebView.Visibility = Visibility.Collapsed;
        ShowState("New tab", "Enter a web address or search term.");
        SetBrowserStatus("New tab ready");
        RebuildTabStrip();
        UpdateNavigationState();
        FocusAddressBox();
    }

    private void BookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        BookmarkGlyph.Fill = BookmarkGlyph.Fill == Brushes.Transparent
            ? TryFindResource("AccentStrongBrush") as Brush ?? Brushes.Transparent
            : Brushes.Transparent;
        SetBrowserStatus("Bookmark persistence will be available with collection browser state.");
    }

    private void ExtractPageButton_Click(object sender, RoutedEventArgs e)
    {
        SetBrowserStatus("Page extraction will be available after browser capture is connected.");
    }

    private void DownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        SetBrowserStatus("Collection downloads will be available after browser sandboxing is connected.");
    }

    private void BrowserMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new();
        menu.Items.Add(new MenuItem { Header = "History and bookmarks will use collection browser state.", IsEnabled = false });
        menu.PlacementTarget = BrowserMenuButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void SettingsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new();
        menu.Items.Add(new MenuItem { Header = "Store tabs between launches", IsCheckable = true, IsChecked = true, IsEnabled = false });
        menu.Items.Add(new MenuItem { Header = "Collection sandbox settings will be connected next.", IsEnabled = false });
        menu.PlacementTarget = SettingsMenuButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void BrowserWebView_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        isNavigating = false;
        if (e.IsSuccess)
        {
            StatePanel.Visibility = Visibility.Collapsed;
            BrowserWebView.Visibility = Visibility.Visible;
            AddressTextBox.Text = BrowserWebView.CoreWebView2?.Source ?? AddressTextBox.Text;
            SetBrowserStatus(BrowserWebView.CoreWebView2?.DocumentTitle);
            RebuildTabStrip();
            UpdateNavigationState();
            return;
        }

        logger?.LogWarning("Browser navigation completed with error. Uri={Uri} WebErrorStatus={WebErrorStatus}", BrowserWebView.Source, e.WebErrorStatus);
        ShowState("Failed to open page", $"WebView2 could not open the page. Error: {e.WebErrorStatus}");
        SetBrowserStatus(null);
        UpdateNavigationState();
    }

    private void RebuildTabStrip()
    {
        TabsPanel.Children.Clear();
        Button tab = new()
        {
            MinWidth = 90,
            MaxWidth = 220,
            Height = 24,
            Padding = new Thickness(8, 0, 8, 0),
            Content = CreateVisibleTabTitle(),
            ToolTip = AddressTextBox.Text,
        };
        tab.Click += (_, _) => FocusAddressBox();
        TabsPanel.Children.Add(tab);
    }

    private void UpdateNavigationState()
    {
        CoreWebView2? coreWebView = BrowserWebView.CoreWebView2;
        BackButton.IsEnabled = coreWebView?.CanGoBack == true;
        ForwardButton.IsEnabled = coreWebView?.CanGoForward == true;
        ReloadButton.IsEnabled = coreWebView is not null;
        StopButton.IsEnabled = isNavigating;
        BookmarkButton.IsEnabled = GetCurrentPageUrl() is not null;
        ExtractPageButton.IsEnabled = GetCurrentPageUrl() is not null && !isNavigating;
        DownloadsButton.IsEnabled = true;
        BrowserMenuButton.IsEnabled = true;
        SettingsMenuButton.IsEnabled = true;
    }

    private void SetBrowserStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            BrowserStatusText.Text = string.Empty;
            BrowserStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        BrowserStatusText.Text = message;
        BrowserStatusText.Visibility = Visibility.Visible;
    }

    private void ShowState(string title, string message)
    {
        BrowserWebView.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
    }

    private string? GetCurrentPageUrl()
    {
        string? source = BrowserWebView.CoreWebView2?.Source;
        return IsAllowedBrowserUri(source) ? source : null;
    }

    private string CreateVisibleTabTitle()
    {
        string? title = BrowserWebView.CoreWebView2?.DocumentTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Length <= 28 ? title : string.Concat(title.AsSpan(0, 25), "...");
        }

        string? currentUrl = GetCurrentPageUrl();
        if (Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return "New tab";
    }

    private static bool IsAllowedBrowserUri(string? uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
            && parsed.Scheme is "http" or "https";
    }

    private static string? NormalizeAddress(string address)
    {
        string value = address.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute)
            && absolute.Scheme is "http" or "https")
        {
            return absolute.AbsoluteUri;
        }

        if (!value.Contains(' ', StringComparison.Ordinal)
            && value.Contains('.', StringComparison.Ordinal))
        {
            return $"https://{value.TrimStart('/')}";
        }

        return $"https://www.bing.com/search?q={Uri.EscapeDataString(value)}";
    }

    private static void OpenPathWithShell(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }
}
