using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed class NoteInspectorViewModel : ObservableObject
{
    private const int AsyncOutlineParseThreshold = 200_000;
    private const int OutlineParseDebounceMilliseconds = 200;
    private readonly MainViewModel mainViewModel;
    private readonly ILogger<NoteInspectorViewModel>? logger;
    private readonly Dictionary<string, NoteOutlineCacheEntry> outlineCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? outlineParseCancellation;
    private ObservableCollection<NoteOutlineItemViewModel> outlineItems = [];
    private NoteOutlineItemViewModel? activeOutlineItem;
    private string activeOutlineBlockId = string.Empty;

    public NoteInspectorViewModel(
        MainViewModel mainViewModel,
        GraphViewModel graphViewModel,
        ILogger<NoteInspectorViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        ArgumentNullException.ThrowIfNull(graphViewModel);

        this.mainViewModel = mainViewModel;
        this.logger = logger;
        GraphViewModel = graphViewModel;

        RefreshCommand = new RelayCommand(Refresh);
        NavigateToOutlineItemCommand = new RelayCommand(NavigateToOutlineItem);
        mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
    }

    public ObservableCollection<NoteOutlineItemViewModel> OutlineItems
    {
        get => outlineItems;
        private set
        {
            outlineItems = value;
            OnPropertyChanged();
        }
    }

    public GraphViewModel GraphViewModel { get; }

    public ICommand RefreshCommand { get; }

    public ICommand NavigateToOutlineItemCommand { get; }

    public NoteOutlineItemViewModel? ActiveOutlineItem
    {
        get => activeOutlineItem;
        private set => SetProperty(ref activeOutlineItem, value);
    }

    public bool HasOutline => OutlineItems.Count > 0;

    public bool ShouldShowOutlineState => !HasOutline;

    public string OutlineSummary => HasOutline
        ? CountDisplayFormatter.Format(OutlineItems.Count, "heading")
        : string.Empty;

    public string OutlineStateTitle
    {
        get
        {
            if (!mainViewModel.HasOpenCollection)
            {
                return "No collection open";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "No note selected"
                : "No headings";
        }
    }

    public string OutlineStateMessage
    {
        get
        {
            if (!mainViewModel.HasOpenCollection)
            {
                return "Select a collection to inspect note structure.";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "Open a note to show its outline."
                : "Add headings to this note to build an outline.";
        }
    }

    internal void Activate()
    {
        Refresh();
    }

    internal void Deactivate()
    {
    }

    internal event EventHandler<NoteOutlineNavigationRequestedEventArgs>? OutlineNavigationRequested;

    internal void UpdateCurrentContent(string content)
    {
        UpdateCurrentContent(mainViewModel.CurrentNotePath, content);
    }

    internal void UpdateCurrentContent(string? notePath, string content)
    {
        QueueOutlineLoadForNote(notePath, content, force: false);
    }

    internal void UpdateActiveOutlineBlock(string? notePath, string? blockId)
    {
        if (!IsActiveNotePath(notePath))
        {
            return;
        }

        activeOutlineBlockId = blockId?.Trim() ?? string.Empty;
        RefreshActiveOutlineItem();
    }

    private void Refresh()
    {
        QueueOutlineLoadForActiveNote(
            string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? string.Empty
                : mainViewModel.GetCurrentEditorContent?.Invoke() ?? string.Empty,
            force: true);
    }

    private void NavigateToOutlineItem(object? parameter)
    {
        if (parameter is not NoteOutlineItemViewModel item
            || string.IsNullOrWhiteSpace(item.BlockId))
        {
            return;
        }

        UpdateActiveOutlineBlock(mainViewModel.CurrentNotePath, item.BlockId);
        OutlineNavigationRequested?.Invoke(this, new NoteOutlineNavigationRequestedEventArgs(item));
    }

    private void QueueOutlineLoadForActiveNote(string content, bool force)
    {
        QueueOutlineLoadForNote(mainViewModel.CurrentNotePath, content, force);
    }

    private void QueueOutlineLoadForNote(string? notePath, string content, bool force)
    {
        string cacheKey = CreateOutlineCacheKey(notePath);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            outlineParseCancellation?.Cancel();
            outlineParseCancellation?.Dispose();
            outlineParseCancellation = null;
            ApplyOutlineItems([]);
            return;
        }

        if (!IsActiveNotePath(notePath))
        {
            return;
        }

        outlineParseCancellation?.Cancel();
        outlineParseCancellation?.Dispose();
        outlineParseCancellation = null;

        string contentHash = CreateContentHash(content);
        if (!force
            && outlineCache.TryGetValue(cacheKey, out NoteOutlineCacheEntry? cached)
            && string.Equals(cached.ContentHash, contentHash, StringComparison.Ordinal))
        {
            ApplyOutlineItems(cached.Items);
            return;
        }

        CancellationTokenSource cancellation = new();
        CancellationToken cancellationToken = cancellation.Token;
        outlineParseCancellation = cancellation;
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(OutlineParseDebounceMilliseconds, cancellationToken);
                return NoteOutlineParser.Parse(content);
            },
            cancellationToken)
            .ContinueWith(
                task =>
                {
                    if (task.IsCanceled
                        || task.IsFaulted
                        || cancellationToken.IsCancellationRequested
                        || !ReferenceEquals(outlineParseCancellation, cancellation))
                    {
                        if (task.IsFaulted)
                        {
                            logger?.LogError(
                                task.Exception,
                                "Note outline parse failed. ContentLength={ContentLength}",
                                content.Length);
                        }

                        return;
                    }

                    outlineCache[cacheKey] = new NoteOutlineCacheEntry(contentHash, task.Result);
                    ApplyOutlineItemsOnUiThread(notePath, task.Result, cancellation, cancellationToken);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void ApplyOutlineItemsOnUiThread(
        string? notePath,
        IReadOnlyList<NoteOutlineItemViewModel> items,
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        void ApplyIfCurrent()
        {
            if (!cancellationToken.IsCancellationRequested
                && ReferenceEquals(outlineParseCancellation, cancellation)
                && IsActiveNotePath(notePath))
            {
                ApplyOutlineItems(items);
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ApplyIfCurrent);
            return;
        }

        ApplyIfCurrent();
    }

    private void ApplyOutlineItems(IReadOnlyList<NoteOutlineItemViewModel> items)
    {
        int previousCount = OutlineItems.Count;
        int sharedCount = Math.Min(previousCount, items.Count);

        for (int index = 0; index < sharedCount; index++)
        {
            NoteOutlineItemViewModel current = OutlineItems[index];
            NoteOutlineItemViewModel next = items[index];
            if (!AreEquivalentOutlineItems(current, next))
            {
                OutlineItems[index] = next;
            }
        }

        for (int index = OutlineItems.Count - 1; index >= items.Count; index--)
        {
            OutlineItems.RemoveAt(index);
        }

        for (int index = sharedCount; index < items.Count; index++)
        {
            OutlineItems.Add(items[index]);
        }

        if (previousCount != OutlineItems.Count)
        {
            NotifyOutlineChanged();
        }

        RefreshActiveOutlineItem();
    }

    private static bool AreEquivalentOutlineItems(
        NoteOutlineItemViewModel left,
        NoteOutlineItemViewModel right)
    {
        return left.Level == right.Level
            && string.Equals(left.Title, right.Title, StringComparison.Ordinal)
            && string.Equals(left.BlockId, right.BlockId, StringComparison.Ordinal);
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentNoteRelativePath)
            or nameof(MainViewModel.CurrentNotePath)
            or nameof(MainViewModel.OpenCollectionPath))
        {
            activeOutlineBlockId = string.Empty;
            ActiveOutlineItem = null;
            RefreshOutlineForActiveNote();
            NotifyOutlineChanged();
        }
    }

    private void RefreshOutlineForActiveNote()
    {
        string content = string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
            ? string.Empty
            : mainViewModel.GetCurrentEditorContent?.Invoke() ?? string.Empty;
        QueueOutlineLoadForActiveNote(content, force: false);
    }

    private void RefreshActiveOutlineItem()
    {
        ActiveOutlineItem = string.IsNullOrWhiteSpace(activeOutlineBlockId)
            ? null
            : OutlineItems.FirstOrDefault(item =>
                string.Equals(item.BlockId, activeOutlineBlockId, StringComparison.Ordinal));
    }

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(ShouldShowOutlineState));
        OnPropertyChanged(nameof(OutlineSummary));
        OnPropertyChanged(nameof(OutlineStateTitle));
        OnPropertyChanged(nameof(OutlineStateMessage));
    }

    private string CreateOutlineCacheKey(string? notePath)
    {
        string? collectionPath = mainViewModel.OpenCollectionPath;
        string? normalizedNotePath = NormalizePath(notePath);
        return string.IsNullOrWhiteSpace(collectionPath) || string.IsNullOrWhiteSpace(normalizedNotePath)
            ? string.Empty
            : $"{NormalizePath(collectionPath) ?? collectionPath.Trim()}|{normalizedNotePath}";
    }

    private bool IsActiveNotePath(string? notePath)
    {
        string? activePath = NormalizePath(mainViewModel.CurrentNotePath);
        string? sourcePath = NormalizePath(notePath);
        return string.IsNullOrWhiteSpace(activePath)
            ? string.IsNullOrWhiteSpace(sourcePath)
            : string.Equals(activePath, sourcePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string CreateContentHash(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(hash);
    }

    private sealed record NoteOutlineCacheEntry(
        string ContentHash,
        IReadOnlyList<NoteOutlineItemViewModel> Items);
}

public sealed class NoteOutlineNavigationRequestedEventArgs : EventArgs
{
    public NoteOutlineNavigationRequestedEventArgs(NoteOutlineItemViewModel item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public NoteOutlineItemViewModel Item { get; }

    public string BlockId => Item.BlockId;
}
