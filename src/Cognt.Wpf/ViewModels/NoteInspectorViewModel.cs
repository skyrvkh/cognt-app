using System.Collections.ObjectModel;
using System.ComponentModel;
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
        QueueOutlineLoadForActiveNote(content, force: false);
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


        OutlineNavigationRequested?.Invoke(this, new NoteOutlineNavigationRequestedEventArgs(item));
    }

    private void QueueOutlineLoadForActiveNote(string content, bool force)
    {
        string cacheKey = CreateActiveOutlineCacheKey();
        QueueOutlineLoad(cacheKey, content, force);
    }

    private void QueueOutlineLoad(string cacheKey, string content, bool force)
    {
        outlineParseCancellation?.Cancel();
        outlineParseCancellation?.Dispose();
        outlineParseCancellation = null;

        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            ApplyOutlineItems([]);
            return;
        }

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

                    ApplyOutlineItemsOnUiThread(task.Result, cancellation, cancellationToken);
                    outlineCache[cacheKey] = new NoteOutlineCacheEntry(contentHash, task.Result);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void ApplyOutlineItemsOnUiThread(
        IReadOnlyList<NoteOutlineItemViewModel> items,
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        void ApplyIfCurrent()
        {
            if (!cancellationToken.IsCancellationRequested
                && ReferenceEquals(outlineParseCancellation, cancellation))
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

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(ShouldShowOutlineState));
        OnPropertyChanged(nameof(OutlineSummary));
        OnPropertyChanged(nameof(OutlineStateTitle));
        OnPropertyChanged(nameof(OutlineStateMessage));
    }

    private string CreateActiveOutlineCacheKey()
    {
        string? collectionPath = mainViewModel.OpenCollectionPath;
        string? relativePath = mainViewModel.CurrentNoteRelativePath;
        return string.IsNullOrWhiteSpace(collectionPath) || string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : $"{collectionPath.Trim()}|{relativePath.Trim()}";
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
