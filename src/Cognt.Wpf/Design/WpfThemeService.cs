using System.IO;
using System.Windows;

namespace CogntApp.Design;

public sealed class WpfThemeService : IThemeService
{
    private const string ThemeDictionaryPrefix = "Themes/Theme.";
    private readonly string settingsPath;

    public WpfThemeService()
    {
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

        for (int index = 0; index < dictionaries.Count; index++)
        {
            string? existingSource = dictionaries[index].Source?.OriginalString;
            if (existingSource is not null
                && existingSource.StartsWith(ThemeDictionaryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[index] = dictionary;
                return;
            }
        }

        dictionaries.Insert(0, dictionary);
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
                return ApplicationTheme.Light;
            }

            return Enum.TryParse(text, ignoreCase: true, out ApplicationTheme theme)
                ? theme
                : ApplicationTheme.Light;
        }
        catch
        {
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
        catch
        {
        }
    }
}
