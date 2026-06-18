using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using CogntApp.Shell.Browser;
using CogntApp.Shell.Dialogs;
using Microsoft.Extensions.Logging;

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
    private readonly IShellDialogService? shellDialogs;
    private readonly Dictionary<string, BrowserTabSession> tabSessions = new(StringComparer.Ordinal);

    private CollectionBrowserState browserState = CollectionBrowserState.CreateEmpty();
    private CollectionBrowserSandbox? sandbox;
    private string? activeTabId;
    private string? loadedCollectionPath;

    public BrowserPaneView(ILogger<BrowserPaneView>? logger = null)
        : this(null, null, null, logger, null, null)
    {
    }

    internal BrowserPaneView(
        CollectionBrowserEnvironmentProvider? environmentProvider,
        CollectionBrowserStateService? stateService,
        Func<string?>? collectionPathProvider,
        ILogger<BrowserPaneView>? logger = null,
        Func<BrowserPageExtraction, Task<string>>? browserPageNoteCreator = null,
        IShellDialogService? shellDialogs = null)
    {
        this.environmentProvider = environmentProvider;
        this.stateService = stateService;
        this.collectionPathProvider = collectionPathProvider;
        this.browserPageNoteCreator = browserPageNoteCreator;
        this.shellDialogs = shellDialogs;
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

    internal void RefreshTheme()
    {
        BrowserOverflowMenu.IsOpen = false;
        RebuildTabStrip();
        UpdateNavigationState();
    }

    internal async Task UseCollectionAsync(string? collectionPath)
    {
        string? normalizedCollectionPath = NormalizeCollectionPathOrNull(collectionPath);
        if (string.Equals(loadedCollectionPath, normalizedCollectionPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await SaveBrowserStateAsync().ConfigureAwait(true);
        DisposeTabSessions();
        loadedCollectionPath = null;
        sandbox = null;
        browserState = CollectionBrowserState.CreateEmpty();
        activeTabId = null;
        AddressTextBox.Text = string.Empty;

        if (normalizedCollectionPath is null || environmentProvider is null || stateService is null)
        {
            ShowState("Browser", "Open a collection to use the browser.");
            RebuildTabStrip();
            UpdateNavigationState();
            return;
        }

        try
        {
            sandbox = await environmentProvider.GetSandboxAsync(normalizedCollectionPath).ConfigureAwait(true);
            browserState = await stateService.LoadAsync(normalizedCollectionPath).ConfigureAwait(true);
            loadedCollectionPath = sandbox.CollectionPath;

            if (browserState.Tabs.Count == 0)
            {
                CreateNewActiveTab(DefaultHomePageUrl);
            }
            else
            {
                activeTabId = browserState.ActiveTabId ?? browserState.Tabs[0].Id;
                browserState.ActiveTabId = activeTabId;
            }

            RebuildTabStrip();
            await ShowActiveTabAsync(loadSavedAddress: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to initialize collection browser. CollectionPath={CollectionPath}", normalizedCollectionPath);
            ShowState("Browser unavailable", ex.Message);
        }

        UpdateNavigationState();
    }

    internal async Task NavigateAsync(string address)
    {
        string? target = NormalizeAddress(address);
        if (target is null)
        {
            ShowState("Browser", "Enter a web address or search term.");
            return;
        }

        try
        {
            if (!await EnsureCollectionReadyAsync().ConfigureAwait(true))
            {
                return;
            }

            CollectionBrowserTabState tab = EnsureActiveTab(target);
            BrowserTabSession session = GetOrCreateTabSession(tab.Id);
            tab.Url = target;
            tab.LastVisitedUtc = DateTimeOffset.UtcNow;
            session.LastError = null;

            ShowTabSession(session);
            AddressTextBox.Text = target;
            SetBrowserStatus($"Loading {target}");
            HideBrowserError();
            session.Browser.Load(target);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Browser navigation failed before dispatch. Target={Target}", target);
            ShowBrowserError($"Could not open this page. {ex.Message}");
            UpdateNavigationState();
        }
    }

    internal async Task OpenPdfAsync(string? title, string documentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        string fullPath = Path.GetFullPath(documentPath);
        if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("The PDF file could not be found.", fullPath);
        }

        if (!await EnsureCollectionReadyAsync().ConfigureAwait(true))
        {
            return;
        }

        string uri = new Uri(fullPath).AbsoluteUri;
        CollectionBrowserTabState tab = CreateNewActiveTab(uri);
        tab.Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fullPath) : title.Trim();
        RebuildTabStrip();
        await NavigateAsync(uri).ConfigureAwait(true);
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
            UpdateNavigationState();
            return false;
        }

        if (!string.Equals(loadedCollectionPath, collectionPath, StringComparison.OrdinalIgnoreCase) || sandbox is null)
        {
            await UseCollectionAsync(collectionPath).ConfigureAwait(true);
        }

        return sandbox is not null;
    }

    private BrowserTabSession GetOrCreateTabSession(string tabId)
    {
        if (tabSessions.TryGetValue(tabId, out BrowserTabSession? existing))
        {
            return existing;
        }

        ChromiumWebBrowser browser = new()
        {
            RequestContext = sandbox?.RequestContext,
            Visibility = Visibility.Collapsed,
        };
        browser.BrowserSettings = new BrowserSettings
        {
            JavascriptAccessClipboard = CefState.Disabled,
            JavascriptDomPaste = CefState.Disabled,
            WindowlessFrameRate = 45,
        };

        BrowserTabSession session = new(tabId, browser);
        browser.LoadingStateChanged += (_, e) => Dispatcher.BeginInvoke(
            () => HandleLoadingStateChanged(session, e));
        browser.AddressChanged += (_, e) => Dispatcher.BeginInvoke(
            () => HandleAddressChanged(session, e.NewValue as string));
        browser.TitleChanged += (_, e) => Dispatcher.BeginInvoke(
            () => HandleTitleChanged(session, e.NewValue as string));
        browser.LoadError += (_, e) => HandleLoadError(session, e);
        browser.LifeSpanHandler = new BrowserPopupHandler(
            targetUrl => Dispatcher.BeginInvoke(() => _ = NavigateInNewTabAsync(targetUrl)));
        browser.MenuHandler = new BrowserContextMenuHandler(
            () => browser.Address,
            command => Dispatcher.BeginInvoke(() => ExecuteBrowserContextMenuCommand(command)));
        if (sandbox is not null)
        {
            browser.DownloadHandler = new CollectionDownloadHandler(
                sandbox,
                () => browser.Address,
                ConfirmAnnotatedPdfOverwriteAsync,
                CompleteAnnotatedPdfSaveAsync,
                fileName => Dispatcher.BeginInvoke(() => SetBrowserStatus($"Downloading {fileName}")),
                fileName => Dispatcher.BeginInvoke(() => SetBrowserStatus($"Cancelled download for {fileName}")));
        }

        tabSessions.Add(tabId, session);
        BrowserHostGrid.Children.Insert(0, browser);
        return session;
    }

    private void HandleLoadingStateChanged(BrowserTabSession session, LoadingStateChangedEventArgs e)
    {
        if (!tabSessions.TryGetValue(session.TabId, out BrowserTabSession? current) || !ReferenceEquals(current, session))
        {
            return;
        }

        session.IsLoading = e.IsLoading;
        session.CanGoBack = e.CanGoBack;
        session.CanGoForward = e.CanGoForward;

        if (!e.IsLoading)
        {
            string? url = GetSessionPageUrl(session);
            if (url is not null && session.LastError is null)
            {
                StatePanel.Visibility = Visibility.Collapsed;
                UpdateTabFromSession(session, addHistory: true);
                _ = SaveBrowserStateAsync();
            }
        }

        if (ReferenceEquals(session, ActiveSession))
        {
            SetBrowserStatus(e.IsLoading ? $"Loading {session.Browser.Address}" : session.Browser.Title);
            UpdateNavigationState();
        }
    }

    private void HandleAddressChanged(BrowserTabSession session, string? address)
    {
        CollectionBrowserTabState? tab = FindTab(session.TabId);
        if (tab is not null && IsAllowedNavigationUri(address))
        {
            tab.Url = address!;
            tab.LastVisitedUtc = DateTimeOffset.UtcNow;
        }

        if (ReferenceEquals(session, ActiveSession) && !AddressTextBox.IsKeyboardFocusWithin)
        {
            AddressTextBox.Text = address ?? string.Empty;
        }
    }

    private void HandleTitleChanged(BrowserTabSession session, string? title)
    {
        CollectionBrowserTabState? tab = FindTab(session.TabId);
        if (tab is null)
        {
            return;
        }

        tab.Title = string.IsNullOrWhiteSpace(title) ? CreateVisibleTabTitle(tab) : title.Trim();
        if (ReferenceEquals(session, ActiveSession))
        {
            SetBrowserStatus(tab.Title);
        }

        RebuildTabStrip();
    }

    private void HandleLoadError(BrowserTabSession session, LoadErrorEventArgs e)
    {
        if (!e.Frame.IsMain || e.ErrorCode == CefErrorCode.Aborted)
        {
            return;
        }

        string message = string.IsNullOrWhiteSpace(e.ErrorText)
            ? "The page could not be loaded."
            : e.ErrorText;
        session.LastError = message;
        logger?.LogWarning(
            "Browser navigation failed. TabId={TabId} Uri={Uri} ErrorCode={ErrorCode} ErrorText={ErrorText}",
            session.TabId,
            e.FailedUrl,
            e.ErrorCode,
            e.ErrorText);

        if (ReferenceEquals(session, ActiveSession))
        {
            Dispatcher.BeginInvoke(() =>
            {
                ShowBrowserError($"Could not load this page. {message}");
                UpdateNavigationState();
            });
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveSession?.Browser.Back();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveSession?.Browser.Forward();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveSession?.Browser.Reload();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveSession?.Browser.Stop();
    }

    private async void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await NavigateAsync(AddressTextBox.Text).ConfigureAwait(true);
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        CreateNewActiveTab(null);
        AddressTextBox.Text = string.Empty;
        ShowState("New tab", "Enter a web address or search term.");
        SetBrowserStatus(null);
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

    private void BrowserMenuButton_Click(object sender, RoutedEventArgs e)
    {
        BrowserOverflowMenu.PlacementTarget = BrowserMenuButton;
        BrowserOverflowMenu.IsOpen = true;
    }

    private void BrowserOverflowMenu_Opened(object sender, RoutedEventArgs e)
    {
        bool isWebPage = IsAllowedWebUri(GetCurrentPageUrl());
        bool isLocalPdf = TryGetActiveLocalPdfPath(out _);
        ExtractPageMenuItem.IsEnabled = CanExtractCurrentPage();
        ExtractPageMenuItem.Visibility = isWebPage ? Visibility.Visible : Visibility.Collapsed;
        RevealPdfMenuItem.Visibility = isLocalPdf ? Visibility.Visible : Visibility.Collapsed;
        ReloadPdfMenuItem.Visibility = isLocalPdf ? Visibility.Visible : Visibility.Collapsed;
        BrowserOverflowActionSeparator.Visibility = isWebPage || isLocalPdf ? Visibility.Visible : Visibility.Collapsed;
        OpenDownloadsMenuItem.IsEnabled = sandbox is not null;
        BrowserProfileMenuItem.Header = sandbox is null
            ? "No collection profile loaded"
            : $"Collection profile: {sandbox.SandboxId}";
        PopulateNavigationMenu(BrowserBookmarksMenuItem, browserState.Bookmarks, item => item.Title, item => item.Url);
        PopulateNavigationMenu(BrowserHistoryMenuItem, browserState.History.Take(12), item => item.Title, item => item.Url);
    }

    private void PopulateNavigationMenu<T>(
        MenuItem parent,
        IEnumerable<T> items,
        Func<T, string> titleSelector,
        Func<T, string> urlSelector)
    {
        parent.Items.Clear();
        foreach (T item in items)
        {
            string url = urlSelector(item);
            MenuItem child = new() { Header = titleSelector(item), ToolTip = url };
            child.Click += async (_, _) => await NavigateAsync(url).ConfigureAwait(true);
            parent.Items.Add(child);
        }

        if (parent.Items.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "No items yet", IsEnabled = false });
        }
    }

    private async void ExtractPageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExtractCurrentPageToNoteAsync().ConfigureAwait(true);
    }

    private void OpenDownloadsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenDownloadsFolder();
    }

    private void RevealPdfMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RevealActiveLocalPdf();
    }

    private void ReloadPdfMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ReloadActiveLocalPdf();
    }

    private void OpenDownloadsFolder()
    {
        if (sandbox is null)
        {
            return;
        }

        OpenPathWithShell(sandbox.DownloadsPath);
    }

    private void ExecuteBrowserContextMenuCommand(BrowserContextMenuCommand command)
    {
        switch (command)
        {
            case BrowserContextMenuCommand.ExtractPage:
                _ = ExtractCurrentPageToNoteAsync();
                break;
            case BrowserContextMenuCommand.RevealLocalPdf:
                RevealActiveLocalPdf();
                break;
            case BrowserContextMenuCommand.ReloadLocalPdf:
                ReloadActiveLocalPdf();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported browser context menu command.");
        }
    }

    private async Task<bool> ConfirmAnnotatedPdfOverwriteAsync(PdfAnnotationSaveRequest request)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            if (shellDialogs is null)
            {
                return false;
            }

            return shellDialogs.ConfirmWarning(
                "Save PDF annotations",
                $"Replace the imported PDF \"{Path.GetFileName(request.SourcePath)}\" with the annotated version? This cannot be undone.");
        }).Task.ConfigureAwait(false);
    }

    private async Task CompleteAnnotatedPdfSaveAsync(PdfAnnotationSaveRequest request)
    {
        PdfAnnotationSaveResult result = await Task.Run(() => PdfAnnotationSaveCoordinator.Complete(request)).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            if (result.ReplacedOriginal)
            {
                ReloadPdfSessions(request.SourcePath);
                SetBrowserStatus($"Saved annotations to {Path.GetFileName(request.SourcePath)}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.FallbackPath))
            {
                string fallbackName = Path.GetFileName(result.FallbackPath);
                SetBrowserStatus($"Could not replace the original. Saved annotated PDF to downloads as {fallbackName}.");
                shellDialogs?.ShowWarning(
                    "PDF annotations saved separately",
                    $"Cognt could not replace \"{Path.GetFileName(request.SourcePath)}\". The annotated PDF was saved to the collection downloads folder as \"{fallbackName}\".");
                return;
            }

            ShowBrowserError($"Could not save PDF annotations. {result.ErrorMessage}");
        }).Task.ConfigureAwait(false);
    }

    private void RevealActiveLocalPdf()
    {
        if (TryGetActiveLocalPdfPath(out string? path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
    }

    private void ReloadActiveLocalPdf()
    {
        if (TryGetActiveLocalPdfPath(out _))
        {
            ActiveSession?.Browser.Reload(ignoreCache: true);
        }
    }

    private void ReloadPdfSessions(string sourcePath)
    {
        foreach (BrowserTabSession session in tabSessions.Values)
        {
            CollectionBrowserTabState? tab = FindTab(session.TabId);
            if (tab is not null
                && TryGetLocalPdfPath(tab.Url, out string? tabPath)
                && string.Equals(tabPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                session.Browser.Reload(ignoreCache: true);
            }
        }
    }

    private void DismissErrorButton_Click(object sender, RoutedEventArgs e)
    {
        HideBrowserError();
    }

    private void RebuildTabStrip()
    {
        TabsPanel.Children.Clear();
        IReadOnlyList<CollectionBrowserTabState> tabs = browserState.Tabs.Count == 0
            ? [new CollectionBrowserTabState { Id = "new", Title = "New tab" }]
            : browserState.Tabs;

        foreach (CollectionBrowserTabState tabState in tabs)
        {
            bool isActive = string.Equals(tabState.Id, activeTabId, StringComparison.Ordinal);
            Border shell = new()
            {
                Height = 26,
                MinWidth = 92,
                MaxWidth = 190,
                Margin = new Thickness(0, 0, 4, 0),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
            };
            shell.SetResourceReference(
                BackgroundProperty,
                isActive ? "BrowserTabActiveBackgroundBrush" : "BrowserTabBackgroundBrush");
            shell.SetResourceReference(
                BorderBrushProperty,
                isActive ? "BrowserTabActiveBorderBrush" : "BrowserTabBorderBrush");
            Grid content = new();
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button activate = new()
            {
                Padding = new Thickness(8, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Style = FindResource("BrowserTabButtonStyle") as Style,
                Content = CreateVisibleTabTitle(tabState),
                ToolTip = tabState.Url,
                Tag = tabState.Id,
            };
            activate.SetResourceReference(
                ForegroundProperty,
                isActive ? "BrowserTabActiveForegroundBrush" : "BrowserTabInactiveForegroundBrush");
            activate.Click += async (_, _) => await ActivateTabAsync(tabState.Id).ConfigureAwait(true);
            content.Children.Add(activate);

            if (tabState.Id != "new")
            {
                Button close = new()
                {
                    Width = 24,
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Style = FindResource("BrowserTabCloseButtonStyle") as Style,
                    ToolTip = "Close tab",
                    Tag = tabState.Id,
                    Content = new System.Windows.Shapes.Path
                    {
                        Width = 8,
                        Height = 8,
                        Data = Geometry.Parse("M4,4 L16,16 M16,4 L4,16"),
                        Stretch = Stretch.Uniform,
                        StrokeThickness = 1.8,
                    },
                };
                ((System.Windows.Shapes.Path)close.Content).SetResourceReference(
                    System.Windows.Shapes.Shape.StrokeProperty,
                    "BrowserTabCloseBrush");
                close.Click += async (_, _) => await CloseTabAsync(tabState.Id).ConfigureAwait(true);
                Grid.SetColumn(close, 1);
                content.Children.Add(close);
            }

            shell.Child = content;
            TabsPanel.Children.Add(shell);
        }
    }

    private async Task ActivateTabAsync(string tabId)
    {
        if (tabId == "new")
        {
            NewTabButton_Click(this, new RoutedEventArgs());
            return;
        }

        activeTabId = tabId;
        browserState.ActiveTabId = tabId;
        RebuildTabStrip();
        await ShowActiveTabAsync(loadSavedAddress: true).ConfigureAwait(true);
        await SaveBrowserStateAsync().ConfigureAwait(true);
    }

    private async Task CloseTabAsync(string tabId)
    {
        int index = browserState.Tabs.FindIndex(tab => string.Equals(tab.Id, tabId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        if (tabSessions.Remove(tabId, out BrowserTabSession? session))
        {
            BrowserHostGrid.Children.Remove(session.Browser);
            session.Browser.Dispose();
        }

        browserState.Tabs.RemoveAt(index);
        if (string.Equals(activeTabId, tabId, StringComparison.Ordinal))
        {
            if (browserState.Tabs.Count == 0)
            {
                CreateNewActiveTab(null);
            }
            else
            {
                activeTabId = browserState.Tabs[Math.Min(index, browserState.Tabs.Count - 1)].Id;
                browserState.ActiveTabId = activeTabId;
            }
        }

        RebuildTabStrip();
        await ShowActiveTabAsync(loadSavedAddress: true).ConfigureAwait(true);
        await SaveBrowserStateAsync().ConfigureAwait(true);
    }

    private Task ShowActiveTabAsync(bool loadSavedAddress)
    {
        CollectionBrowserTabState? tab = ActiveTab;
        if (tab is null || !IsAllowedNavigationUri(tab.Url))
        {
            foreach (BrowserTabSession candidate in tabSessions.Values)
            {
                candidate.Browser.Visibility = Visibility.Collapsed;
            }

            AddressTextBox.Text = string.Empty;
            ShowState("New tab", "Enter a web address or search term.");
            UpdateNavigationState();
            return Task.CompletedTask;
        }

        BrowserTabSession session = GetOrCreateTabSession(tab.Id);
        ShowTabSession(session);
        AddressTextBox.Text = session.Browser.Address ?? tab.Url;
        if (loadSavedAddress && string.IsNullOrWhiteSpace(session.Browser.Address))
        {
            session.Browser.Load(tab.Url);
        }

        UpdateNavigationState();
        return Task.CompletedTask;
    }

    private void ShowTabSession(BrowserTabSession activeSession)
    {
        foreach (BrowserTabSession session in tabSessions.Values)
        {
            session.Browser.Visibility = ReferenceEquals(session, activeSession)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        StatePanel.Visibility = Visibility.Collapsed;
        if (activeSession.LastError is null)
        {
            HideBrowserError();
        }
        else
        {
            ShowBrowserError($"Could not load this page. {activeSession.LastError}");
        }
    }

    private void UpdateNavigationState()
    {
        BrowserTabSession? session = ActiveSession;
        BackButton.IsEnabled = session?.CanGoBack == true;
        ForwardButton.IsEnabled = session?.CanGoForward == true;
        ReloadButton.IsEnabled = session is not null;
        ReloadButton.Visibility = session?.IsLoading == true ? Visibility.Collapsed : Visibility.Visible;
        StopButton.IsEnabled = session?.IsLoading == true;
        StopButton.Visibility = session?.IsLoading == true ? Visibility.Visible : Visibility.Collapsed;
        BookmarkButton.IsEnabled = GetCurrentPageUrl() is not null;
        if (IsCurrentPageBookmarked())
        {
            BookmarkGlyph.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentStrongBrush");
        }
        else
        {
            BookmarkGlyph.Fill = Brushes.Transparent;
        }
    }

    private void SetBrowserStatus(string? message)
    {
        BrowserStatusText.Text = message ?? string.Empty;
        BrowserStatusText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ShowState(string title, string message)
    {
        foreach (BrowserTabSession session in tabSessions.Values)
        {
            session.Browser.Visibility = Visibility.Collapsed;
        }

        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
        HideBrowserError();
    }

    private void ShowBrowserError(string message)
    {
        ErrorBannerText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
        SetBrowserStatus(null);
    }

    private void HideBrowserError()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
        ErrorBannerText.Text = string.Empty;
    }

    private string? GetCurrentPageUrl()
    {
        return ActiveSession is { } session
            ? GetSessionPageUrl(session)
            : IsAllowedNavigationUri(ActiveTab?.Url) ? ActiveTab!.Url : null;
    }

    private static string? GetSessionPageUrl(BrowserTabSession session)
    {
        return IsAllowedNavigationUri(session.Browser.Address) ? session.Browser.Address : null;
    }

    private BrowserTabSession? ActiveSession =>
        activeTabId is not null && tabSessions.TryGetValue(activeTabId, out BrowserTabSession? session)
            ? session
            : null;

    private CollectionBrowserTabState? ActiveTab => FindTab(activeTabId);

    private CollectionBrowserTabState? FindTab(string? tabId)
    {
        return browserState.Tabs.FirstOrDefault(tab => string.Equals(tab.Id, tabId, StringComparison.Ordinal));
    }

    private CollectionBrowserTabState EnsureActiveTab(string url)
    {
        CollectionBrowserTabState tab = ActiveTab ?? CreateNewActiveTab(url);
        tab.Url = url;
        tab.LastVisitedUtc = DateTimeOffset.UtcNow;
        browserState.ActiveTabId = tab.Id;
        return tab;
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

    private void UpdateTabFromSession(BrowserTabSession session, bool addHistory)
    {
        string? url = GetSessionPageUrl(session);
        CollectionBrowserTabState? tab = FindTab(session.TabId);
        if (url is null || tab is null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(session.Browser.Title) ? url : session.Browser.Title.Trim();
        tab.Url = url;
        tab.Title = title;
        tab.LastVisitedUtc = DateTimeOffset.UtcNow;
        session.LastError = null;

        if (addHistory && !string.Equals(session.LastHistoryUrl, url, StringComparison.OrdinalIgnoreCase))
        {
            browserState.History.Insert(0, new CollectionBrowserHistoryEntry
            {
                Url = url,
                Title = title,
                VisitedUtc = DateTimeOffset.UtcNow,
            });
            session.LastHistoryUrl = url;
            if (browserState.History.Count > MaxHistoryEntries)
            {
                browserState.History.RemoveRange(MaxHistoryEntries, browserState.History.Count - MaxHistoryEntries);
            }
        }

        if (ReferenceEquals(session, ActiveSession))
        {
            AddressTextBox.Text = url;
            HideBrowserError();
        }

        RebuildTabStrip();
    }

    private bool IsCurrentPageBookmarked()
    {
        string? url = GetCurrentPageUrl();
        return url is not null && browserState.Bookmarks.Any(
            bookmark => string.Equals(bookmark.Url, url, StringComparison.OrdinalIgnoreCase));
    }

    private string CreateVisibleTabTitle()
    {
        return ActiveTab is { } tab ? CreateVisibleTabTitle(tab) : "New tab";
    }

    private static string CreateVisibleTabTitle(CollectionBrowserTabState tab)
    {
        string title = string.IsNullOrWhiteSpace(tab.Title) ? tab.Url : tab.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return "New tab";
        }

        return title.Length <= 28 ? title : $"{title[..27]}...";
    }

    private async Task NavigateInNewTabAsync(string? url)
    {
        if (!IsAllowedNavigationUri(url))
        {
            return;
        }

        CreateNewActiveTab(url);
        RebuildTabStrip();
        await NavigateAsync(url!).ConfigureAwait(true);
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
            logger?.LogWarning(ex, "Failed to save collection browser state. CollectionPath={CollectionPath}", loadedCollectionPath);
        }
    }

    private void DisposeTabSessions()
    {
        foreach (BrowserTabSession session in tabSessions.Values)
        {
            BrowserHostGrid.Children.Remove(session.Browser);
            session.Browser.Dispose();
        }

        tabSessions.Clear();
    }

    private static string? NormalizeAddress(string? value)
    {
        string address = value?.Trim() ?? string.Empty;
        if (address.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(address, UriKind.Absolute, out Uri? absolute)
            && (absolute.Scheme is "http" or "https" || IsLocalPdfUri(absolute)))
        {
            return absolute.AbsoluteUri;
        }

        if (!address.Any(char.IsWhiteSpace)
            && address.Contains('.', StringComparison.Ordinal)
            && Uri.TryCreate($"https://{address}", UriKind.Absolute, out Uri? hostUri))
        {
            return hostUri.AbsoluteUri;
        }

        return $"https://www.google.com/search?q={Uri.EscapeDataString(address)}";
    }

    private static bool IsAllowedNavigationUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme is "http" or "https" || IsLocalPdfUri(uri));
    }

    private static bool IsAllowedWebUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https";
    }

    private static bool IsLocalPdfUri(Uri uri)
    {
        return uri.IsFile
            && string.Equals(Path.GetExtension(uri.LocalPath), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetActiveLocalPdfPath(out string? path)
    {
        return TryGetLocalPdfPath(GetCurrentPageUrl(), out path);
    }

    private static bool TryGetLocalPdfPath(string? value, out string? path)
    {
        path = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || !IsLocalPdfUri(uri))
        {
            return false;
        }

        path = Path.GetFullPath(uri.LocalPath);
        return File.Exists(path);
    }

    private static string? NormalizeCollectionPathOrNull(string? collectionPath)
    {
        return string.IsNullOrWhiteSpace(collectionPath)
            ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(collectionPath));
    }

    private static void OpenPathWithShell(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private sealed class BrowserTabSession(string tabId, ChromiumWebBrowser browser)
    {
        internal string TabId { get; } = tabId;
        internal ChromiumWebBrowser Browser { get; } = browser;
        internal bool IsLoading { get; set; }
        internal bool CanGoBack { get; set; }
        internal bool CanGoForward { get; set; }
        internal string? LastError { get; set; }
        internal string? LastHistoryUrl { get; set; }
    }
}
