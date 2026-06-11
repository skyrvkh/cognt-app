using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace CogntApp.Design;

public sealed class WpfThemeService : IThemeService
{
    private const string ThemeDictionaryPrefix = "Themes/Theme.";
    private readonly string settingsPath;
    private readonly ILogger<WpfThemeService> logger;

    public WpfThemeService(ILogger<WpfThemeService> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cognt",
            "theme.txt");
        CurrentTheme = LoadTheme();
        ApplyThemeResources(CurrentTheme);
    }

    public ApplicationTheme CurrentTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public void ApplyTheme(ApplicationTheme theme)
    {
        if (theme == CurrentTheme)
        {
            return;
        }

        ApplyThemeResources(theme);
        CurrentTheme = theme;
        SaveTheme(theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyThemeResources(ApplicationTheme theme)
    {
        Application? application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        Uri source = CreateThemeUri(theme);
        ResourceDictionary dictionary = new() { Source = source };

        int themeDictionaryIndex = -1;
        for (int index = dictionaries.Count - 1; index >= 0; index--)
        {
            string? existingSource = dictionaries[index].Source?.OriginalString;
            if (IsThemeDictionarySource(existingSource))
            {
                themeDictionaryIndex = index;
                dictionaries.RemoveAt(index);
            }
        }

        dictionaries.Insert(themeDictionaryIndex >= 0 ? Math.Min(themeDictionaryIndex, dictionaries.Count) : 0, dictionary);
    }

    private static bool IsThemeDictionarySource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return source.Replace('\\', '/').Contains(ThemeDictionaryPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri CreateThemeUri(ApplicationTheme theme)
    {
        string name = theme switch
        {
            ApplicationTheme.Light => "Light",
            ApplicationTheme.Graphite => "Graphite",
            _ => "Light"
        };

        return new Uri($"{ThemeDictionaryPrefix}{name}.xaml", UriKind.Relative);
    }

    private ApplicationTheme LoadTheme()
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return ApplicationTheme.Light;
            }

            string text = File.ReadAllText(settingsPath).Trim();
            if (string.Equals(text, "Focus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Field", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Theme settings migrated legacy theme value to Light. SettingsPath={SettingsPath} StoredValue={StoredValue}", settingsPath, text);
                return ApplicationTheme.Light;
            }

            if (Enum.TryParse(text, ignoreCase: true, out ApplicationTheme theme))
            {
                return theme;
            }

            logger.LogWarning("Theme settings value was invalid; using default theme. SettingsPath={SettingsPath} StoredValue={StoredValue}", settingsPath, text);
            return ApplicationTheme.Light;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Theme settings load failed; using default theme. SettingsPath={SettingsPath}", settingsPath);
            return ApplicationTheme.Light;
        }
    }

    private void SaveTheme(ApplicationTheme theme)
    {
        try
        {
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(settingsPath, theme.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Theme settings save failed. SettingsPath={SettingsPath} Theme={Theme}", settingsPath, theme);
        }
    }
}
