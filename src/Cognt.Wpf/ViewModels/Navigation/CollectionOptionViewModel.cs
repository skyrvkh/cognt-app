using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Navigation;

public sealed class CollectionOptionViewModel : ObservableObject
{
    private bool isSelected;

    public CollectionOptionViewModel(
        string id,
        string name,
        string path,
        bool isSelected,
        int noteCount,
        int cardCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Id = id;
        Name = name;
        Path = path;
        this.isSelected = isSelected;
        NoteCount = Math.Max(0, noteCount);
        CardCount = Math.Max(0, cardCount);
    }

    public string Id { get; }

    public string Name { get; }

    public string Path { get; }

    public int NoteCount { get; }

    public int CardCount { get; }

    public string CountsSummary =>
        $"{CountDisplayFormatter.Format(NoteCount, "note")} • {CountDisplayFormatter.Format(CardCount, "card")}";

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
