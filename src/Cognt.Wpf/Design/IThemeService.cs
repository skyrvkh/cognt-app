namespace CogntApp.Design;

public interface IThemeService
{
    event EventHandler? ThemeChanged;

    ApplicationTheme CurrentTheme { get; }

    void ApplyTheme(ApplicationTheme theme);
}
