using System.Collections.ObjectModel;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed class NoteTreeNodeViewModel : ObservableObject
{
    private bool isExpanded;
    private bool isSelected;
    private bool isMultiSelected;
    private bool areChildrenLoaded;
    private bool isRenaming;
    private string? notePath;
    private string title;
    private string editingTitle;

    public NoteTreeNodeViewModel(
        long id,
        long? parentId,
        KnowledgeItemKind kind,
        string title,
        string? fileRelPath,
        string? notePath,
        int childCount,
        bool isPlaceholder = false,
        long? flashcardCardId = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        Id = id;
        ParentId = parentId;
        Kind = kind;
        this.title = title;
        editingTitle = this.title;
        FileRelPath = fileRelPath;
        ChildCount = Math.Max(0, childCount);
        IsPlaceholder = isPlaceholder;
        FlashcardCardId = flashcardCardId;
        this.notePath = notePath;
        areChildrenLoaded = isPlaceholder || ChildCount == 0;
    }

    public long Id { get; }

    public long? ParentId { get; private set; }

    public KnowledgeItemKind Kind { get; }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string Prefix => IsPlaceholder
        ? string.Empty
        : IsFlashcardNode
            ? "[Card]"
        : Kind switch
        {
            KnowledgeItemKind.Note => "[N]",
            KnowledgeItemKind.PdfDocument => "[PDF]",
            _ => "[F]"
        };

    public string EditingTitle
    {
        get => editingTitle;
        set => SetProperty(ref editingTitle, value);
    }

    public string? FileRelPath { get; }

    public long? FlashcardCardId { get; }

    public bool IsFlashcardNode => FlashcardCardId.HasValue;

    public int ChildCount { get; private set; }

    public bool IsPlaceholder { get; }

    public bool CanContainChildren => !IsPlaceholder
        && !IsFlashcardNode
        && Kind is KnowledgeItemKind.Folder
            or KnowledgeItemKind.Note
            or KnowledgeItemKind.Root
            or KnowledgeItemKind.Trash;

    public ObservableCollection<NoteTreeNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsMultiSelected
    {
        get => isMultiSelected;
        set => SetProperty(ref isMultiSelected, value);
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
        ArgumentNullException.ThrowIfNull(newTitle);

        Title = newTitle;

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
            KnowledgeItemKind.Note,
            string.Empty,
            fileRelPath: null,
            notePath: null,
            childCount: 0,
            isPlaceholder: true);
    }

    public static NoteTreeNodeViewModel CreateFlashcardNode(
        long cardId,
        long parentId,
        string title,
        string? sourceRelPath)
    {
        return new NoteTreeNodeViewModel(
            id: -Math.Abs(cardId),
            parentId,
            KnowledgeItemKind.Note,
            title,
            fileRelPath: sourceRelPath,
            notePath: null,
            childCount: 0,
            isPlaceholder: false,
            flashcardCardId: cardId);
    }
}
