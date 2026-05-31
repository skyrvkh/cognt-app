using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CogntApp.Design;

public sealed class WpfEditorSettingsService : IEditorSettingsService
{
    private const string PreferencesDirectoryName = ".cognt-preferences";
    private const string EditorSettingsFileName = "editor-settings.json";
    private const string CollectionSettingsFileName = ".set";
    private const string CollectionSettingsKind = "cognt.collection.settings";
    private const int CollectionSettingsVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string fallbackSettingsPath;
    private readonly ILogger<WpfEditorSettingsService> logger;

    public WpfEditorSettingsService(ILogger<WpfEditorSettingsService> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        fallbackSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cognt",
            EditorSettingsFileName);
    }

    public EditorSettings Load(string? collectionPath = null)
    {
        try
        {
            if (TryResolveCollectionSettingsPath(collectionPath, out string collectionSettingsPath))
            {
                if (File.Exists(collectionSettingsPath))
                {
                    EditorSettings settings = LoadCollectionSettingsFile(collectionSettingsPath);
                    return settings;
                }

                EditorSettings migratedSettings = LoadLegacyCollectionSettings(collectionPath) ?? LoadFallback();
                Save(migratedSettings, collectionPath);
                return migratedSettings.Normalize();
            }

            EditorSettings fallback = LoadFallback();
            return fallback;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Editor settings load failed; using defaults. CollectionPath={CollectionPath}", collectionPath);
            return EditorSettings.Default;
        }
    }

    public void Save(EditorSettings settings, string? collectionPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (TryResolveCollectionSettingsPath(collectionPath, out string collectionSettingsPath))
            {
                WriteCollectionSettingsFile(collectionSettingsPath, settings);
                return;
            }

            string settingsPath = fallbackSettingsPath;
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                settingsPath,
                JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Editor settings save failed. CollectionPath={CollectionPath}", collectionPath);
        }
    }

    public void EnsureCollectionSettingsFile(string collectionPath)
    {
        try
        {
            if (!TryResolveCollectionSettingsPath(collectionPath, out string collectionSettingsPath)
                || File.Exists(collectionSettingsPath))
            {
                return;
            }

            EditorSettings settings = LoadLegacyCollectionSettings(collectionPath) ?? LoadFallback();
            WriteCollectionSettingsFile(collectionSettingsPath, settings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ensure collection editor settings failed. CollectionPath={CollectionPath}", collectionPath);
        }
    }

    private EditorSettings LoadFallback()
    {
        try
        {
            if (!File.Exists(fallbackSettingsPath))
            {
                return EditorSettings.Default;
            }

            EditorSettings? settings = JsonSerializer.Deserialize<EditorSettings>(
                File.ReadAllText(fallbackSettingsPath),
                JsonOptions);
            return (settings ?? EditorSettings.Default).Normalize();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallback editor settings load failed; using defaults. SettingsPath={SettingsPath}", fallbackSettingsPath);
            return EditorSettings.Default;
        }
    }

    private EditorSettings? LoadLegacyCollectionSettings(string? collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || !Directory.Exists(collectionPath))
        {
            return null;
        }

        try
        {
            string legacySettingsPath = Path.Combine(collectionPath, PreferencesDirectoryName, EditorSettingsFileName);
            if (!File.Exists(legacySettingsPath))
            {
                return null;
            }

            EditorSettings? settings = JsonSerializer.Deserialize<EditorSettings>(
                File.ReadAllText(legacySettingsPath),
                JsonOptions);
            return settings?.Normalize();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Legacy collection editor settings load failed. CollectionPath={CollectionPath}", collectionPath);
            return null;
        }
    }

    private EditorSettings LoadCollectionSettingsFile(string settingsPath)
    {
        string json = File.ReadAllText(settingsPath);
        CollectionSettingsFile? collectionSettings = JsonSerializer.Deserialize<CollectionSettingsFile>(json, JsonOptions);
        if (collectionSettings is not null
            && string.Equals(collectionSettings.Kind, CollectionSettingsKind, StringComparison.Ordinal)
            && collectionSettings.Editor is not null)
        {
            return collectionSettings.Editor.Normalize();
        }

        EditorSettings? directSettings = JsonSerializer.Deserialize<EditorSettings>(json, JsonOptions);
        return (directSettings ?? EditorSettings.Default).Normalize();
    }

    private static bool TryResolveCollectionSettingsPath(string? collectionPath, out string settingsPath)
    {
        if (!string.IsNullOrWhiteSpace(collectionPath) && Directory.Exists(collectionPath))
        {
            settingsPath = Path.Combine(collectionPath, CollectionSettingsFileName);
            return true;
        }

        settingsPath = string.Empty;
        return false;
    }

    private static void WriteCollectionSettingsFile(string settingsPath, EditorSettings settings)
    {
        string? directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CollectionSettingsFile collectionSettings = new()
        {
            Kind = CollectionSettingsKind,
            Version = CollectionSettingsVersion,
            Editor = settings.Normalize(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(collectionSettings, JsonOptions));
    }

    private sealed class CollectionSettingsFile
    {
        public string? Kind { get; init; }

        public int Version { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public EditorSettings? Editor { get; init; }
    }
}
