using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Collections;

internal sealed class FileSystemKnownCollectionStore : IKnownCollectionStore
{
    private const string StoreDirectoryName = "Cognt";
    private const string StoreFileName = "collections.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string storePath;
    private readonly ILogger<FileSystemKnownCollectionStore> logger;

    public FileSystemKnownCollectionStore(ILogger<FileSystemKnownCollectionStore> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appDataPath))
        {
            appDataPath = Environment.CurrentDirectory;
        }

        storePath = Path.Combine(appDataPath, StoreDirectoryName, StoreFileName);
    }

    public IReadOnlyList<string> LoadCollectionPaths()
    {
        try
        {
            CollectionStoreDocument store = LoadStore();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<string> normalizedPaths = [];

            foreach (string path in store.CollectionPaths)
            {
                if (TryNormalizePath(path, out string normalizedPath)
                    && !ContainsNormalizedPath(store.HiddenCollectionPaths, normalizedPath)
                    && seen.Add(normalizedPath))
                {
                    normalizedPaths.Add(normalizedPath);
                }
            }

            if (TryNormalizePath(store.RecentCollectionPath, out string recentPath)
                && !ContainsNormalizedPath(store.HiddenCollectionPaths, recentPath)
                && seen.Add(recentPath))
            {
                normalizedPaths.Add(recentPath);
            }

            return normalizedPaths;
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(ex, "Known collection paths load recovered from store failure. StorePath={StorePath}", storePath);
            return [];
        }
    }

    public string? LoadRecentCollectionPath()
    {
        try
        {
            CollectionStoreDocument store = LoadStore();
            string? recent = TryNormalizePath(store.RecentCollectionPath, out string recentPath)
                && !ContainsNormalizedPath(store.HiddenCollectionPaths, recentPath)
                ? recentPath
                : null;
            return recent;
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(ex, "Recent collection path load recovered from store failure. StorePath={StorePath}", storePath);
            return null;
        }
    }

    public void RememberCollectionPath(string path)
    {
        if (!TryNormalizePath(path, out string normalizedPath))
        {
            return;
        }

        CollectionStoreDocument store = LoadStoreOrDefault("remember collection path");
        List<string> paths = NormalizePathSet(store.CollectionPaths).ToList();

        if (!paths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(normalizedPath);
        }

        string[] hiddenPaths = NormalizePathSet(store.HiddenCollectionPaths)
            .Where(candidate => !string.Equals(candidate, normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SaveCollectionStore(paths, normalizedPath, hiddenPaths);
    }

    public void ForgetCollectionPath(string path)
    {
        if (!TryNormalizePath(path, out string normalizedPath))
        {
            return;
        }

        CollectionStoreDocument store = LoadStoreOrDefault("forget collection path");
        string[] paths = NormalizePathSet(store.CollectionPaths)
            .Where(candidate => !string.Equals(candidate, normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string? recentPath = TryNormalizePath(store.RecentCollectionPath, out string loadedRecentPath)
            ? loadedRecentPath
            : null;
        if (string.Equals(recentPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            recentPath = null;
        }

        SaveCollectionStore(paths, recentPath, store.HiddenCollectionPaths);
    }

    public void HideCollectionPath(string path)
    {
        if (!TryNormalizePath(path, out string normalizedPath))
        {
            return;
        }

        CollectionStoreDocument store = LoadStoreOrDefault("hide collection path");
        string[] paths = NormalizePathSet(store.CollectionPaths)
            .Where(candidate => !string.Equals(candidate, normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        List<string> hiddenPaths = [.. NormalizePathSet(store.HiddenCollectionPaths)];
        if (!hiddenPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            hiddenPaths.Add(normalizedPath);
        }

        string? recentPath = TryNormalizePath(store.RecentCollectionPath, out string loadedRecentPath)
            ? loadedRecentPath
            : null;
        if (string.Equals(recentPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            recentPath = null;
        }

        SaveCollectionStore(paths, recentPath, hiddenPaths);
    }

    public void UnhideCollectionPath(string path)
    {
        if (!TryNormalizePath(path, out string normalizedPath))
        {
            return;
        }

        CollectionStoreDocument store = LoadStoreOrDefault("unhide collection path");
        string[] hiddenPaths = NormalizePathSet(store.HiddenCollectionPaths)
            .Where(candidate => !string.Equals(candidate, normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SaveCollectionStore(store.CollectionPaths, store.RecentCollectionPath, hiddenPaths);
    }

    public bool IsCollectionPathHidden(string path)
    {
        if (!TryNormalizePath(path, out string normalizedPath))
        {
            return false;
        }

        try
        {
            return ContainsNormalizedPath(LoadStore().HiddenCollectionPaths, normalizedPath);
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(ex, "Hidden collection path load recovered from store failure. StorePath={StorePath}", storePath);
            return false;
        }
    }

    private CollectionStoreDocument LoadStore()
    {
        if (!File.Exists(storePath))
        {
            return new CollectionStoreDocument();
        }

        string json = File.ReadAllText(storePath);
        try
        {
            CollectionStoreDocument? store = JsonSerializer.Deserialize<CollectionStoreDocument>(json, JsonOptions);
            if (store is null)
            {
                logger.LogWarning("Known collection store deserialized to null. StorePath={StorePath}", storePath);
                return new CollectionStoreDocument();
            }

            return store;
        }
        catch (JsonException)
        {
            logger.LogInformation("Known collection store is in legacy array format. StorePath={StorePath}", storePath);
            string[]? legacyPaths = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
            return new CollectionStoreDocument
            {
                CollectionPaths = legacyPaths ?? [],
            };
        }
    }

    private CollectionStoreDocument LoadStoreOrDefault(string operation)
    {
        try
        {
            return LoadStore();
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(
                ex,
                "Known collection store operation recovered from store load failure. Operation={Operation} StorePath={StorePath}",
                operation,
                storePath);
            return new CollectionStoreDocument();
        }
    }

    private void SaveCollectionStore(
        IEnumerable<string> paths,
        string? recentCollectionPath,
        IEnumerable<string>? hiddenCollectionPaths = null)
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(storePath) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(directoryPath);

            string[] normalizedPaths = paths
                .Select(path => TryNormalizePath(path, out string normalizedPath) ? normalizedPath : string.Empty)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string? normalizedRecentPath = TryNormalizePath(recentCollectionPath, out string recentPath)
                ? recentPath
                : null;
            string[] normalizedHiddenPaths = NormalizePathSet(hiddenCollectionPaths ?? []);

            normalizedPaths = normalizedPaths
                .Where(path => !normalizedHiddenPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(normalizedRecentPath)
                && normalizedHiddenPaths.Contains(normalizedRecentPath, StringComparer.OrdinalIgnoreCase))
            {
                normalizedRecentPath = null;
            }

            if (!string.IsNullOrWhiteSpace(normalizedRecentPath)
                && !normalizedPaths.Contains(normalizedRecentPath, StringComparer.OrdinalIgnoreCase))
            {
                normalizedPaths = [.. normalizedPaths, normalizedRecentPath];
            }

            CollectionStoreDocument store = new()
            {
                RecentCollectionPath = normalizedRecentPath,
                CollectionPaths = normalizedPaths,
                HiddenCollectionPaths = normalizedHiddenPaths,
            };

            File.WriteAllText(storePath, JsonSerializer.Serialize(store, JsonOptions));

        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(ex, "Known collection store save recovered from store failure. StorePath={StorePath}", storePath);
        }
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));

    private static string[] NormalizePathSet(IEnumerable<string?> paths)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedPaths = [];
        foreach (string? path in paths)
        {
            if (TryNormalizePath(path, out string normalizedPath) && seen.Add(normalizedPath))
            {
                normalizedPaths.Add(normalizedPath);
            }
        }

        normalizedPaths.Sort(StringComparer.OrdinalIgnoreCase);
        return normalizedPaths.ToArray();
    }

    private static bool ContainsNormalizedPath(IEnumerable<string?> paths, string normalizedPath)
        => NormalizePathSet(paths).Contains(normalizedPath, StringComparer.OrdinalIgnoreCase);

    private static bool TryNormalizePath(string? path, out string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            normalizedPath = string.Empty;
            return false;
        }

        try
        {
            normalizedPath = NormalizePath(path);
            return true;
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static bool IsRecoverableStoreException(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or JsonException
            or NotSupportedException
            or UnauthorizedAccessException;
    }

    private sealed class CollectionStoreDocument
    {
        public string? RecentCollectionPath { get; init; }

        public string[] CollectionPaths { get; init; } = [];

        public string[] HiddenCollectionPaths { get; init; } = [];
    }
}
