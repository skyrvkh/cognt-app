using System.IO;
using System.Text.Json;

namespace CogntApp.Design;

public sealed class WpfEditorSettingsService : IEditorSettingsService
{
    private const string PreferencesDirectoryName = ".cognt-preferences";
    private const string EditorSettingsFileName = "editor-settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string fallbackSettingsPath;

    public WpfEditorSettingsService()
    {
        fallbackSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cognt",
            EditorSettingsFileName);
    }

    public EditorSettings Load(string? collectionPath = null)
    {
        try
        {
            string settingsPath = ResolveSettingsPath(collectionPath);
            if (!File.Exists(settingsPath))
            {
                return LoadFallback();
            }

            EditorSettings? settings = JsonSerializer.Deserialize<EditorSettings>(
                File.ReadAllText(settingsPath),
                JsonOptions);
            return (settings ?? EditorSettings.Default).Normalize();
        }
        catch
        {
            return EditorSettings.Default;
        }
    }

    public void Save(EditorSettings settings, string? collectionPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string settingsPath = ResolveSettingsPath(collectionPath);
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                settingsPath,
                JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
        }
        catch
        {
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
        catch
        {
            return EditorSettings.Default;
        }
    }

    private string ResolveSettingsPath(string? collectionPath)
    {
        if (!string.IsNullOrWhiteSpace(collectionPath) && Directory.Exists(collectionPath))
        {
            return Path.Combine(collectionPath, PreferencesDirectoryName, EditorSettingsFileName);
        }

        return fallbackSettingsPath;
    }
}
