using System.Collections.ObjectModel;

namespace CogntApp.ViewModels;

public sealed class NoteTreeNodeViewModel : ObservableObject
{
    private bool isExpanded;
    private string? notePath;

    public NoteTreeNodeViewModel(string header, string path, bool isMarkdownFile)
    {
        Header = header;
        Path = path;
        IsMarkdownFile = isMarkdownFile;
    }

    public string Header { get; }

    public string Path { get; }

    public bool IsMarkdownFile { get; }

    public ObservableCollection<NoteTreeNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public string? NotePath
    {
        get => notePath;
        set => SetProperty(ref notePath, value);
    }
}
