using CogntApp.Design;

namespace CogntApp.ViewModels;

public sealed class ThemeOptionViewModel
{
    public ThemeOptionViewModel(ApplicationTheme theme, string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Theme = theme;
        Name = name;
        Description = description;
    }

    public ApplicationTheme Theme { get; }

    public string Name { get; }

    public string Description { get; }
}
