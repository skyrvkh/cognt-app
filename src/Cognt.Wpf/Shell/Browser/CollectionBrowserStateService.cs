using System.IO;
using System.Text.Json;

namespace CogntApp.Shell.Browser;

public sealed class CollectionBrowserStateService
{
    private const string StateFileName = "browser-state.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly Func<DateTimeOffset> utcNow;

    public CollectionBrowserStateService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    internal CollectionBrowserStateService(Func<DateTimeOffset> utcNow)
    {
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    internal async Task<CollectionBrowserState> LoadAsync(
        string collectionPath,
        CancellationToken cancellationToken = default)
    {
        string statePath = GetStateFilePath(collectionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);

        if (!File.Exists(statePath))
        {
            CollectionBrowserState emptyState = CollectionBrowserState.CreateEmpty();
            await SaveAsync(collectionPath, emptyState, cancellationToken).ConfigureAwait(false);
            return emptyState;
        }

        try
        {
            string json = await File.ReadAllTextAsync(statePath, cancellationToken).ConfigureAwait(false);
            return NormalizeState(JsonSerializer.Deserialize<CollectionBrowserState>(json, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            MoveCorruptStateFile(statePath);
            CollectionBrowserState emptyState = CollectionBrowserState.CreateEmpty();
            await SaveAsync(collectionPath, emptyState, cancellationToken).ConfigureAwait(false);
            return emptyState;
        }
    }

    internal async Task SaveAsync(
        string collectionPath,
        CollectionBrowserState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        string statePath = GetStateFilePath(collectionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);

        string json = JsonSerializer.Serialize(CreatePersistableState(state), JsonOptions);
        await File.WriteAllTextAsync(statePath, json, cancellationToken).ConfigureAwait(false);
    }

    internal string GetStateFilePath(string collectionPath)
    {
        string normalizedCollectionPath = CollectionBrowserEnvironmentProvider.NormalizeExistingCollectionPath(collectionPath);
        return Path.Combine(
            normalizedCollectionPath,
            CollectionBrowserEnvironmentProvider.CollectionBrowserDirectoryName,
            StateFileName);
    }

    private static CollectionBrowserState CreatePersistableState(CollectionBrowserState state)
    {
        CollectionBrowserState normalized = NormalizeState(state);
        return normalized.Settings.StoreSession
            ? normalized
            : normalized with { ActiveTabId = null, Tabs = [] };
    }

    private static CollectionBrowserState NormalizeState(CollectionBrowserState? state)
    {
        state ??= CollectionBrowserState.CreateEmpty();
        state.Tabs.RemoveAll(tab => string.IsNullOrWhiteSpace(tab.Id));
        state.History.RemoveAll(entry => string.IsNullOrWhiteSpace(entry.Url));
        state.Bookmarks.RemoveAll(bookmark => string.IsNullOrWhiteSpace(bookmark.Id) || string.IsNullOrWhiteSpace(bookmark.Url));

        if (!string.IsNullOrWhiteSpace(state.ActiveTabId)
            && !state.Tabs.Any(tab => string.Equals(tab.Id, state.ActiveTabId, StringComparison.Ordinal)))
        {
            state.ActiveTabId = state.Tabs.FirstOrDefault()?.Id;
        }

        return state;
    }

    private void MoveCorruptStateFile(string statePath)
    {
        string directoryPath = Path.GetDirectoryName(statePath)!;
        string timestamp = utcNow().ToUniversalTime().ToString("yyyyMMddHHmmssfff");
        string corruptPath = Path.Combine(directoryPath, $"browser-state.corrupt-{timestamp}.json");
        int suffix = 2;

        while (File.Exists(corruptPath))
        {
            corruptPath = Path.Combine(directoryPath, $"browser-state.corrupt-{timestamp}-{suffix}.json");
            suffix++;
        }

        File.Move(statePath, corruptPath);
    }
}

internal sealed record CollectionBrowserState
{
    public string? ActiveTabId { get; set; }

    public List<CollectionBrowserTabState> Tabs { get; init; } = [];

    public List<CollectionBrowserHistoryEntry> History { get; init; } = [];

    public List<CollectionBrowserBookmark> Bookmarks { get; init; } = [];

    public CollectionBrowserSettings Settings { get; init; } = new();

    internal static CollectionBrowserState CreateEmpty()
    {
        return new CollectionBrowserState();
    }
}

internal sealed record CollectionBrowserTabState
{
    public string Id { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset LastVisitedUtc { get; set; }
}

internal sealed record CollectionBrowserHistoryEntry
{
    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset VisitedUtc { get; set; }
}

internal sealed record CollectionBrowserBookmark
{
    public string Id { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
}

internal sealed record CollectionBrowserSettings
{
    public bool StoreSession { get; set; } = true;
}
