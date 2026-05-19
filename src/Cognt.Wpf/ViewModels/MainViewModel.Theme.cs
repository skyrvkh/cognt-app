using CogntApp.Design;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void InitializeThemeOptions()
    {
        ThemeOptions.Add(new ThemeOptionViewModel(
            ApplicationTheme.Light,
            "Light",
            "Clean light surfaces with a crisp blue command color."));
        ThemeOptions.Add(new ThemeOptionViewModel(
            ApplicationTheme.Graphite,
            "Graphite",
            "Dark neutral surfaces with high-contrast teal commands."));

        selectedThemeOption = ThemeOptions.First(option => option.Theme == themeService.CurrentTheme);
    }
}
