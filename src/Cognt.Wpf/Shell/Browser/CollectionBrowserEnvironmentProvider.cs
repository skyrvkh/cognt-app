using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Shell.Browser;

public sealed class CollectionBrowserEnvironmentProvider
{
    private const string StoreDirectoryName = "Cognt";
    private const string BrowserSandboxesDirectoryName = "browser-sandboxes";
    private const string BrowserProfileDirectoryName = "profile";
    internal const string CollectionBrowserDirectoryName = ".cognt-browser";
    private const string ManifestFileName = "sandbox.json";
    private const string DownloadsDirectoryName = "downloads";
    private const string BrowserArguments =
        "--disable-background-networking " +
        "--disable-sync " +
        "--no-default-browser-check " +
        "--no-first-run";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<string, Task<CollectionBrowserSandbox>> sandboxesByCollection =
        new(StringComparer.OrdinalIgnoreCase);

    internal Task<CollectionBrowserSandbox> GetSandboxAsync(string collectionPath)
    {
        string normalizedCollectionPath = NormalizeExistingCollectionPath(collectionPath);

        lock (sandboxesByCollection)
        {
            if (!sandboxesByCollection.TryGetValue(normalizedCollectionPath, out Task<CollectionBrowserSandbox>? task))
            {
                task = CreateSandboxAsync(normalizedCollectionPath);
                sandboxesByCollection[normalizedCollectionPath] = task;
            }

            return task;
        }
    }

    internal static string NormalizeExistingCollectionPath(string collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            throw new InvalidOperationException("Open a collection before using the browser.");
        }

        string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(collectionPath.Trim()));
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The collection folder does not exist: {fullPath}");
        }

        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    internal static string CreateDownloadPath(
        CollectionBrowserSandbox sandbox,
        string? requestedFilePath,
        string? sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        Directory.CreateDirectory(sandbox.DownloadsPath);

        string fileName = CreateDownloadFileName(requestedFilePath, sourceUri);
        string extension = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string candidatePath = Path.Combine(sandbox.DownloadsPath, fileName);
        int suffix = 2;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(sandbox.DownloadsPath, $"{stem} ({suffix}){extension}");
            suffix++;
        }

        return candidatePath;
    }

    private static async Task<CollectionBrowserSandbox> CreateSandboxAsync(string normalizedCollectionPath)
    {
        CollectionBrowserManifest manifest = LoadOrCreateManifest(normalizedCollectionPath);
        string profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            StoreDirectoryName,
            BrowserSandboxesDirectoryName,
            manifest.SandboxId,
            BrowserProfileDirectoryName);
        string downloadsPath = Path.Combine(
            normalizedCollectionPath,
            CollectionBrowserDirectoryName,
            DownloadsDirectoryName);

        Directory.CreateDirectory(profilePath);
        Directory.CreateDirectory(downloadsPath);

        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: profilePath,
            options: new CoreWebView2EnvironmentOptions(BrowserArguments)).ConfigureAwait(true);

        return new CollectionBrowserSandbox(
            normalizedCollectionPath,
            manifest.SandboxId,
            profilePath,
            downloadsPath,
            environment);
    }

    private static CollectionBrowserManifest LoadOrCreateManifest(string collectionPath)
    {
        string browserDirectoryPath = Path.Combine(collectionPath, CollectionBrowserDirectoryName);
        string manifestPath = Path.Combine(browserDirectoryPath, ManifestFileName);
        Directory.CreateDirectory(browserDirectoryPath);

        if (TryLoadManifest(manifestPath, out CollectionBrowserManifest? manifest))
        {
            return manifest!;
        }

        manifest = new CollectionBrowserManifest(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        return manifest;
    }

    private static bool TryLoadManifest(string manifestPath, out CollectionBrowserManifest? manifest)
    {
        manifest = null;
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            CollectionBrowserManifest? loaded =
                JsonSerializer.Deserialize<CollectionBrowserManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (loaded is null || !IsValidSandboxId(loaded.SandboxId))
            {
                return false;
            }

            manifest = loaded;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string CreateDownloadFileName(string? requestedFilePath, string? sourceUri)
    {
        string? fileName = Path.GetFileName(requestedFilePath);
        if (string.IsNullOrWhiteSpace(fileName) && Uri.TryCreate(sourceUri, UriKind.Absolute, out Uri? uri))
        {
            fileName = Path.GetFileName(Uri.UnescapeDataString(uri.LocalPath));
        }

        fileName = SanitizeFileName(fileName);
        return string.IsNullOrWhiteSpace(fileName) ? "download" : fileName;
    }

    private static string SanitizeFileName(string? fileName)
    {
        string value = string.IsNullOrWhiteSpace(fileName) ? "download" : fileName.Trim();
        StringBuilder builder = new(value.Length);
        HashSet<char> invalidCharacters = [.. Path.GetInvalidFileNameChars()];

        foreach (char character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        string sanitized = builder.ToString().Trim(' ', '.');
        return sanitized.Length == 0 ? "download" : sanitized;
    }

    private static bool IsValidSandboxId(string? sandboxId)
    {
        return !string.IsNullOrWhiteSpace(sandboxId)
            && sandboxId.Length <= 64
            && sandboxId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private sealed record CollectionBrowserManifest(string SandboxId, DateTimeOffset CreatedUtc);
}

internal sealed record CollectionBrowserSandbox(
    string CollectionPath,
    string SandboxId,
    string ProfilePath,
    string DownloadsPath,
    CoreWebView2Environment Environment);
