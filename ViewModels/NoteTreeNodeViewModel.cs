using System.Collections.ObjectModel;
using CogntApp.Models.Tree;

namespace CogntApp.ViewModels;

public sealed class NoteTreeNodeViewModel : ObservableObject
{
    private bool isExpanded;
    private bool areChildrenLoaded;
    private bool isRenaming;
    private string? notePath;
    private string title;
    private string header;
    private string editingTitle;

    public NoteTreeNodeViewModel(
        long id,
        long? parentId,
        TreeItemKind kind,
        string title,
        string? fileRelPath,
        string? notePath,
        int childCount,
        bool isPlaceholder = false)
    {
        Id = id;
        ParentId = parentId;
        Kind = kind;
        this.title = title;
        editingTitle = title;
        FileRelPath = fileRelPath;
        ChildCount = childCount;
        IsPlaceholder = isPlaceholder;
        header = CreateHeader(kind, title, isPlaceholder);
        this.notePath = notePath;
        areChildrenLoaded = isPlaceholder || childCount == 0;
    }

    public long Id { get; }

    public long? ParentId { get; private set; }

    public TreeItemKind Kind { get; }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string Header
    {
        get => header;
        private set => SetProperty(ref header, value);
    }

    public string Prefix => IsPlaceholder
        ? string.Empty
        : Kind == TreeItemKind.Note
            ? "[N]"
            : "[F]";

    public string EditingTitle
    {
        get => editingTitle;
        set => SetProperty(ref editingTitle, value);
    }

    public string Path => NotePath ?? Id.ToString();

    public string? FileRelPath { get; }

    public int ChildCount { get; private set; }

    public bool IsPlaceholder { get; }

    public bool CanContainChildren => !IsPlaceholder
        && Kind is TreeItemKind.Folder or TreeItemKind.Note or TreeItemKind.Root or TreeItemKind.Trash;

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

    public bool AreChildrenLoaded
    {
        get => areChildrenLoaded;
        set => SetProperty(ref areChildrenLoaded, value);
    }

    public bool IsRenaming
    {
        get => isRenaming;
        private set => SetProperty(ref isRenaming, value);
    }

    public void MarkChildrenLoaded()
    {
        AreChildrenLoaded = true;
        ChildCount = Children.Count;
        OnPropertyChanged(nameof(ChildCount));
    }

    public void UpdateTitle(string newTitle)
    {
        Title = newTitle;
        Header = CreateHeader(Kind, newTitle, IsPlaceholder);

        if (!IsRenaming)
        {
            EditingTitle = newTitle;
        }
    }

    public void BeginRename()
    {
        EditingTitle = Title;
        IsRenaming = true;
    }

    public void CancelRename()
    {
        EditingTitle = Title;
        IsRenaming = false;
    }

    public void EndRename()
    {
        IsRenaming = false;
        EditingTitle = Title;
    }

    public void UpdateParent(long? parentId)
    {
        ParentId = parentId;
        OnPropertyChanged(nameof(ParentId));
    }

    public void UpdateChildCount(int childCount)
    {
        ChildCount = Math.Max(0, childCount);
        OnPropertyChanged(nameof(ChildCount));
    }

    public static NoteTreeNodeViewModel CreatePlaceholder()
    {
        return new NoteTreeNodeViewModel(
            id: 0,
            parentId: null,
            TreeItemKind.Note,
            string.Empty,
            fileRelPath: null,
            notePath: null,
            childCount: 0,
            isPlaceholder: true);
    }

    private static string CreateHeader(TreeItemKind kind, string title, bool isPlaceholder)
    {
        return isPlaceholder
            ? string.Empty
            : kind == TreeItemKind.Note
                ? $"[N] {title}"
                : $"[F] {title}";
    }
}
