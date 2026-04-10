using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Navigation;

public sealed class EditorBreadcrumbItemViewModel : ObservableObject
{
    private string title;
    private string editingTitle;
    private bool isEditing;

    public EditorBreadcrumbItemViewModel(
        long id,
        string title,
        string? notePath,
        bool isNote,
        bool isCurrent,
        bool isLast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = id;
        this.title = title;
        editingTitle = title;
        NotePath = notePath;
        IsNote = isNote;
        IsCurrent = isCurrent;
        IsLast = isLast;
    }

    public long Id { get; }

    public string Title
    {
        get => title;
        private set
        {
            if (SetProperty(ref title, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string DisplayTitle => title.Length <= 25 ? title : title[..25] + "...";

    public string EditingTitle
    {
        get => editingTitle;
        set => SetProperty(ref editingTitle, value);
    }

    public string? NotePath { get; }

    public bool IsNote { get; }

    public bool IsCurrent { get; }

    public bool IsLast { get; }

    public string Separator => IsLast ? string.Empty : "/";

    public string KindLabel => IsNote ? "note" : "folder";

    public bool IsEditing
    {
        get => isEditing;
        private set => SetProperty(ref isEditing, value);
    }

    public void BeginEdit()
    {
        EditingTitle = Title;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        EditingTitle = Title;
        IsEditing = false;
    }

    public void CommitTitle(string newTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newTitle);

        Title = newTitle;
        EditingTitle = newTitle;
        IsEditing = false;
    }
}
