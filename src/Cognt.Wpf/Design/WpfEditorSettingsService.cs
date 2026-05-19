using System.IO;
using System.Text.Json;

namespace CogntApp.Design;

public sealed class WpfEditorSettingsService : IEditorSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string settingsPath;

    public WpfEditorSettingsService()
    {
        settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cognt",
            "editor-settings.json");
    }

    public EditorSettings Load()
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return EditorSettings.Default;
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

    public void Save(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
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
}
