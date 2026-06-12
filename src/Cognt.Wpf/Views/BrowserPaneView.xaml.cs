using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CogntApp.Shell.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CogntApp.Views;

public partial class BrowserPaneView : UserControl
{
    private const string DefaultHomePageUrl = "https://www.google.com/";
    private const int MaxHistoryEntries = 200;
    private readonly ILogger<BrowserPaneView>? logger;
    private readonly CollectionBrowserEnvironmentProvider? environmentProvider;
    private readonly CollectionBrowserStateService? stateService;
    private readonly Func<string?>? collectionPathProvider;
    private readonly Func<BrowserPageExtraction, Task<string>>? browserPageNoteCreator;
    private CollectionBrowserState browserState = CollectionBrowserState.CreateEmpty();
    private CollectionBrowserSandbox? sandbox;
    private string? activeTabId;
    private string? loadedCollectionPath;
    private string? initializedSandboxId;
    private bool isNavigating;

    public BrowserPaneView(ILogger<BrowserPaneView>? logger = null)
        : this(null, null, null, logger)
    {
    }

    internal BrowserPaneView(
        CollectionBrowserEnvironmentProvider? environmentProvider,
        CollectionBrowserStateService? stateService,
        Func<string?>? collectionPathProvider,
        ILogger<BrowserPaneView>? logger = null,
        Func<BrowserPageExtraction, Task<string>>? browserPageNoteCreator = null)
    {
        this.environmentProvider = environmentProvider;
        this.stateService = stateService;
        this.collectionPathProvider = collectionPathProvider;
        this.browserPageNoteCreator = browserPageNoteCreator;
        this.logger = logger;
        InitializeComponent();
        ShowEmptySurface();
        RebuildTabStrip();
        UpdateNavigationState();
    }

    internal void FocusAddressBox()
    {
        AddressTextBox.Focus();
        AddressTextBox.SelectAll();
    }

    internal async Task UseCollectionAsync(string? collectionPath)
    {
        string? normalizedCollectionPath = NormalizeCollectionPathOrNull(collectionPath);
        if (string.Equals(loadedCollectionPath, normalizedCollectionPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await SaveBrowserStateAsync().ConfigureAwait(true);
        loadedCollectionPath = null;
        sandbox = null;
        browserState = CollectionBrowserState.CreateEmpty();
        activeTabId = null;
        AddressTextBox.Text = string.Empty;
        ResetBrowserControl();

        if (normalizedCollectionPath is null || environmentProvider is null || stateService is null)
        {
            ShowEmptySurface();
            SetBrowserStatus(normalizedCollectionPath is null ? "Open a collection to use the browser." : null);
            RebuildTabStrip();
            UpdateNavigationState();
            return;
        }

        try
        {
            sandbox = await environmentProvider.GetSandboxAsync(normalizedCollectionPath).ConfigureAwait(true);
            browserState = await stateService.LoadAsync(normalizedCollectionPath).ConfigureAwait(true);
            loadedCollectionPath = sandbox.CollectionPath;
            activeTabId = browserState.ActiveTabId ?? browserState.Tabs.FirstOrDefault()?.Id;
            RebuildTabStrip();

            CollectionBrowserTabState? restoredTab = ActiveTab;
            if (IsAllowedBrowserUri(restoredTab?.Url))
            {
                await NavigateAsync(restoredTab!.Url).ConfigureAwait(true);
            }
            else if (browserState.Tabs.Count == 0)
            {
                CreateNewActiveTab(DefaultHomePageUrl);
                RebuildTabStrip();
                await NavigateAsync(DefaultHomePageUrl).ConfigureAwait(true);
            }
            else
            {
                ShowEmptySurface();
                SetBrowserStatus("Collection browser ready");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to initialize collection browser. CollectionPath={CollectionPath}", normalizedCollectionPath);
            ShowState("Browser unavailable", ex.Message);
            SetBrowserStatus(null);
        }

        UpdateNavigationState();
    }

    internal async Task NavigateAsync(string address)
    {
        string? target = NormalizeAddress(address);
        if (target is null)
        {
            ShowEmptySurface();
            return;
        }

        AddressTextBox.Text = target;
        ShowState("Loading", target);
        SetBrowserStatus($"Loading {target}");

        try
        {
            if (!await EnsureCollectionReadyAsync().ConfigureAwait(true))
            {
                return;
            }

            EnsureActiveTab(target);
            isNavigating = true;
            UpdateNavigationState();
            await EnsureBrowserWebViewAsync().ConfigureAwait(true);
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

    private async Task<bool> EnsureCollectionReadyAsync()
    {
        if (environmentProvider is null || stateService is null || collectionPathProvider is null)
        {
            return true;
        }

        string? collectionPath = NormalizeCollectionPathOrNull(collectionPathProvider());
        if (collectionPath is null)
        {
            ShowState("Browser", "Open a collection to use the browser.");
            SetBrowserStatus(null);
            UpdateNavigationState();
            return false;
        }

        if (!string.Equals(loadedCollectionPath, collectionPath, StringComparison.OrdinalIgnoreCase) || sandbox is null)
        {
            await UseCollectionAsync(collectionPath).ConfigureAwait(true);
        }

        return sandbox is not null;
    }

    private async Task EnsureBrowserWebViewAsync()
    {
        if (sandbox is not null && !string.Equals(initializedSandboxId, sandbox.SandboxId, StringComparison.Ordinal))
        {
            ResetBrowserControl();
        }

        if (BrowserWebView.CoreWebView2 is not null)
        {
            return;
        }

        if (sandbox is null)
        {
            await BrowserWebView.EnsureCoreWebView2Async().ConfigureAwait(true);
        }
        else
        {
            await BrowserWebView.EnsureCoreWebView2Async(sandbox.Environment).ConfigureAwait(true);
            initializedSandboxId = sandbox.SandboxId;
        }

        CoreWebView2 coreWebView = BrowserWebView.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 initialization completed without a CoreWebView2 instance.");
        ConfigureCoreWebView(coreWebView);
        coreWebView.DownloadStarting += CoreWebView2_DownloadStarting;
        coreWebView.NewWindowRequested += CoreWebView2_NewWindowRequested;
    }

    private void ResetBrowserControl()
    {
        if (BrowserWebView.CoreWebView2 is not null)
        {
            BrowserWebView.CoreWebView2.DownloadStarting -= CoreWebView2_DownloadStarting;
            BrowserWebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
        }

        int index = BrowserHostGrid.Children.IndexOf(BrowserWebView);
        BrowserHostGrid.Children.Remove(BrowserWebView);
        BrowserWebView.NavigationCompleted -= BrowserWebView_NavigationCompleted;
        BrowserWebView.Dispose();

        WebView2 replacement = new()
        {
            Visibility = Visibility.Collapsed,
        };
        replacement.NavigationCompleted += BrowserWebView_NavigationCompleted;
        BrowserWebView = replacement;
        BrowserHostGrid.Children.Insert(index < 0 ? 0 : index, BrowserWebView);
        initializedSandboxId = null;
    }

    private static void ConfigureCoreWebView(CoreWebView2 coreWebView)
    {
        coreWebView.Settings.AreDevToolsEnabled = false;
        coreWebView.Settings.IsStatusBarEnabled = true;
        coreWebView.Settings.AreHostObjectsAllowed = false;
        coreWebView.Settings.IsPasswordAutosaveEnabled = false;
        coreWebView.Settings.IsGeneralAutofillEnabled = false;
    }

    private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        if (sandbox is null)
        {
            return;
        }

        string path = CollectionBrowserEnvironmentProvider.CreateDownloadPath(
            sandbox,
            e.ResultFilePath,
            e.DownloadOperation.Uri);
        e.ResultFilePath = path;
        SetBrowserStatus($"Downloading {Path.GetFileName(path)}");
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        _ = NavigateInNewTabAsync(e.Uri);
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
        CreateNewActiveTab(null);
        AddressTextBox.Text = string.Empty;
        ShowEmptySurface();
        SetBrowserStatus("New tab ready");
        RebuildTabStrip();
        UpdateNavigationState();
        FocusAddressBox();
        _ = SaveBrowserStateAsync();
    }

    private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        string? url = GetCurrentPageUrl();
        if (url is null)
        {
            return;
        }

        CollectionBrowserBookmark? existing = browserState.Bookmarks.FirstOrDefault(
            bookmark => string.Equals(bookmark.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            browserState.Bookmarks.Insert(0, new CollectionBrowserBookmark
            {
                Id = Guid.NewGuid().ToString("N"),
                Url = url,
                Title = CreateVisibleTabTitle(),
                CreatedUtc = DateTimeOffset.UtcNow,
            });
            SetBrowserStatus("Bookmarked current page");
        }
        else
        {
            browserState.Bookmarks.Remove(existing);
            SetBrowserStatus("Removed bookmark");
        }

        await SaveBrowserStateAsync().ConfigureAwait(true);
        UpdateNavigationState();
    }

    private async void ExtractPageButton_Click(object sender, RoutedEventArgs e)
    {
        await ExtractCurrentPageToNoteAsync().ConfigureAwait(true);
    }

    private void DownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sandbox is null)
        {
            SetBrowserStatus("Open a collection to use downloads.");
            return;
        }

        OpenPathWithShell(sandbox.DownloadsPath);
    }

    private void BrowserMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new();
        AddBrowserMenuSection(menu, "Bookmarks", browserState.Bookmarks, item => item.Title, item => item.Url);
        AddBrowserMenuSection(menu, "History", browserState.History.Take(10), item => item.Title, item => item.Url);
        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No bookmarks or history yet.", IsEnabled = false });
        }

        menu.PlacementTarget = BrowserMenuButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void SettingsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new();
        MenuItem sessionItem = new()
        {
            Header = "Collection saves tabs, cookies, and sessions",
            IsEnabled = false,
        };
        menu.Items.Add(sessionItem);
        menu.Items.Add(new MenuItem
        {
            Header = sandbox is null ? "No collection sandbox loaded" : $"Sandbox: {sandbox.SandboxId}",
            IsEnabled = false,
        });
        menu.PlacementTarget = SettingsMenuButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async void BrowserWebView_NavigationCompleted(
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
            UpdateActiveTabFromWebView(addHistory: true);
            await SaveBrowserStateAsync().ConfigureAwait(true);
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

        IReadOnlyList<CollectionBrowserTabState> tabs = browserState.Tabs.Count == 0
            ? [new CollectionBrowserTabState { Id = "new", Title = "New tab" }]
            : browserState.Tabs;
        foreach (CollectionBrowserTabState tabState in tabs)
        {
            Button tab = new()
            {
                MinWidth = 90,
                MaxWidth = 220,
                Height = 24,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(8, 0, 8, 0),
                Content = CreateVisibleTabTitle(tabState),
                ToolTip = tabState.Url,
                Tag = tabState.Id,
            };
            if (string.Equals(tabState.Id, activeTabId, StringComparison.Ordinal))
            {
                tab.Background = TryFindResource("AccentSubtleBrush") as Brush ?? Brushes.Transparent;
                tab.BorderBrush = TryFindResource("AccentStrongBrush") as Brush ?? Brushes.Transparent;
            }

            tab.Click += async (_, _) => await ActivateTabAsync(tabState.Id).ConfigureAwait(true);
            TabsPanel.Children.Add(tab);
        }
    }

    private async Task ActivateTabAsync(string tabId)
    {
        if (tabId == "new")
        {
            NewTabButton_Click(this, new RoutedEventArgs());
            return;
        }

        UpdateActiveTabFromWebView(addHistory: false);
        activeTabId = tabId;
        browserState.ActiveTabId = tabId;
        CollectionBrowserTabState? tab = ActiveTab;
        RebuildTabStrip();

        if (IsAllowedBrowserUri(tab?.Url))
        {
            await NavigateAsync(tab!.Url).ConfigureAwait(true);
        }
        else
        {
            ShowEmptySurface();
            FocusAddressBox();
        }
    }

    private void UpdateNavigationState()
    {
        CoreWebView2? coreWebView = BrowserWebView.CoreWebView2;
        BackButton.IsEnabled = coreWebView?.CanGoBack == true;
        ForwardButton.IsEnabled = coreWebView?.CanGoForward == true;
        ReloadButton.IsEnabled = coreWebView is not null;
        StopButton.IsEnabled = isNavigating;
        BookmarkButton.IsEnabled = GetCurrentPageUrl() is not null;
        ExtractPageButton.IsEnabled = CanExtractCurrentPage();
        DownloadsButton.IsEnabled = true;
        BrowserMenuButton.IsEnabled = true;
        SettingsMenuButton.IsEnabled = true;
        BookmarkGlyph.Fill = IsCurrentPageBookmarked()
            ? TryFindResource("AccentStrongBrush") as Brush ?? Brushes.Transparent
            : Brushes.Transparent;
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

    private void ShowEmptySurface()
    {
        BrowserWebView.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Collapsed;
    }

    private string? GetCurrentPageUrl()
    {
        string? source = BrowserWebView.CoreWebView2?.Source;
        return IsAllowedBrowserUri(source) ? source : null;
    }

    private CollectionBrowserTabState? ActiveTab =>
        browserState.Tabs.FirstOrDefault(tab => string.Equals(tab.Id, activeTabId, StringComparison.Ordinal));

    private void EnsureActiveTab(string url)
    {
        CollectionBrowserTabState? tab = ActiveTab;
        if (tab is null)
        {
            tab = CreateNewActiveTab(url);
        }

        tab.Url = url;
        tab.LastVisitedUtc = DateTimeOffset.UtcNow;
        browserState.ActiveTabId = tab.Id;
    }

    private CollectionBrowserTabState CreateNewActiveTab(string? url)
    {
        CollectionBrowserTabState tab = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = url ?? string.Empty,
            Title = "New tab",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastVisitedUtc = DateTimeOffset.UtcNow,
        };
        browserState.Tabs.Add(tab);
        activeTabId = tab.Id;
        browserState.ActiveTabId = tab.Id;
        return tab;
    }

    private void UpdateActiveTabFromWebView(bool addHistory)
    {
        string? url = GetCurrentPageUrl();
        CollectionBrowserTabState? tab = ActiveTab;
        if (url is null || tab is null)
        {
            return;
        }

        string title = BrowserWebView.CoreWebView2?.DocumentTitle ?? url;
        tab.Url = url;
        tab.Title = title;
        tab.LastVisitedUtc = DateTimeOffset.UtcNow;
        if (!addHistory)
        {
            return;
        }

        browserState.History.RemoveAll(entry => string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));
        browserState.History.Insert(0, new CollectionBrowserHistoryEntry
        {
            Url = url,
            Title = title,
            VisitedUtc = DateTimeOffset.UtcNow,
        });
        if (browserState.History.Count > MaxHistoryEntries)
        {
            browserState.History.RemoveRange(MaxHistoryEntries, browserState.History.Count - MaxHistoryEntries);
        }
    }

    private async Task SaveBrowserStateAsync()
    {
        if (stateService is null || string.IsNullOrWhiteSpace(loadedCollectionPath))
        {
            return;
        }

        try
        {
            await stateService.SaveAsync(loadedCollectionPath, browserState).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to save browser state. CollectionPath={CollectionPath}", loadedCollectionPath);
        }
    }

    private async Task NavigateInNewTabAsync(string? uri)
    {
        if (!IsAllowedBrowserUri(uri))
        {
            return;
        }

        CreateNewActiveTab(uri);
        RebuildTabStrip();
        await NavigateAsync(uri!).ConfigureAwait(true);
    }

    private void AddBrowserMenuSection<T>(
        ContextMenu menu,
        string title,
        IEnumerable<T> items,
        Func<T, string> getTitle,
        Func<T, string> getUrl)
    {
        bool addedHeader = false;
        foreach (T item in items)
        {
            if (!addedHeader)
            {
                menu.Items.Add(new MenuItem { Header = title, IsEnabled = false });
                addedHeader = true;
            }

            string url = getUrl(item);
            MenuItem menuItem = new()
            {
                Header = string.IsNullOrWhiteSpace(getTitle(item)) ? url : getTitle(item),
                ToolTip = url,
            };
            menuItem.Click += async (_, _) => await NavigateAsync(url).ConfigureAwait(true);
            menu.Items.Add(menuItem);
        }
    }

    private bool IsCurrentPageBookmarked()
    {
        string? url = GetCurrentPageUrl();
        return url is not null
            && browserState.Bookmarks.Any(bookmark => string.Equals(bookmark.Url, url, StringComparison.OrdinalIgnoreCase));
    }

    private string CreateVisibleTabTitle(CollectionBrowserTabState? tabState = null)
    {
        string? title = tabState?.Title ?? BrowserWebView.CoreWebView2?.DocumentTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Length <= 28 ? title : string.Concat(title.AsSpan(0, 25), "...");
        }

        string? currentUrl = tabState?.Url ?? GetCurrentPageUrl();
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

    private static string? NormalizeCollectionPathOrNull(string? collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            return null;
        }

        try
        {
            return CollectionBrowserEnvironmentProvider.NormalizeExistingCollectionPath(collectionPath);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
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
