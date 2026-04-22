using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using CogntApp.Models.Markdown;
using CogntApp.Models.Navigation;
using CogntApp.Models.Tree;
using CogntApp.Shell.Collections;
using CogntApp.Services;
using KnowledgeOpenedNoteReadModel = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.KnowledgeOpenedNoteReadModel;
using KnowledgeItemKind = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.KnowledgeItemKind;

namespace CogntApp.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IKnowledgeCollectionPathResolver collectionPathResolver = new FileSystemKnowledgeCollectionPathResolver();
    private readonly INoteService noteService;
    private string notesDirectory = string.Empty;
    private string currentNotesDirectory = string.Empty;
    private string? currentNotePath;
    private long? currentNoteItemId;
    private string currentNoteTitle = string.Empty;
    private string lastSavedContent = string.Empty;
    private bool isLoadingNotesDirectory;

    public MainViewModel(INoteService noteService)
    {
        this.noteService = noteService;

        LoadDirectoryCommand = new RelayCommand(
            () => RunUserAction("Load Collection Failed", () => LoadNotesDirectory(NotesDirectory)));
        LoadCollectionCommand = LoadDirectoryCommand;
        ShowDashboardCommand = new RelayCommand(
            () => RunUserAction("Navigation Failed", ShowDashboard));
        ShowAllNotesCommand = new RelayCommand(
            () => RunUserAction("Navigation Failed", ShowAllNotes));
        ShowDecksCommand = new RelayCommand(
            () => RunUserAction("Navigation Failed", ShowDecks));
        AddNoteCommand = new RelayCommand(
            () => RunUserAction("Create Note Failed", AddNote));
        OpenNoteCommand = new RelayCommand(
            parameter => RunUserAction("Open Note Failed", () => OpenNoteFromCommand(parameter)));
    }

    public event EventHandler<OverlayRequestedEventArgs>? OverlayRequested;

    public event EventHandler? ClearEditorRequested;

    public event EventHandler<OpenNoteRequestedEventArgs>? OpenNoteRequested;

    public event EventHandler? MentionTargetsChanged;

    public event EventHandler<UserMessageRequestedEventArgs>? UserMessageRequested;

    public event EventHandler<string>? CurrentNoteTitleChanged;

    public Func<string>? GetCurrentEditorContent { get; set; }

    public ObservableCollection<NoteTreeNodeViewModel> NoteRoots { get; } = [];

    public ObservableCollection<RecentNoteViewModel> RecentNotes { get; } = [];

    public ICommand LoadDirectoryCommand { get; }

    public ICommand LoadCollectionCommand { get; }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowAllNotesCommand { get; }

    public ICommand ShowDecksCommand { get; }

    public ICommand AddNoteCommand { get; }

    public ICommand OpenNoteCommand { get; }

    public string NotesDirectory
    {
        get => notesDirectory;
        set
        {
            if (SetProperty(ref notesDirectory, value))
            {
                OnPropertyChanged(nameof(CollectionPath));
                OnPropertyChanged(nameof(CurrentCollectionPath));
            }
        }
    }

    public string CollectionPath
    {
        get => notesDirectory;
        set
        {
            if (SetProperty(ref notesDirectory, value))
            {
                OnPropertyChanged(nameof(NotesDirectory));
                OnPropertyChanged(nameof(CurrentCollectionPath));
            }
        }
    }

    public string CurrentNotesDirectory => currentNotesDirectory;

    public string OpenCollectionPath => currentNotesDirectory;

    public string CurrentCollectionPath => string.IsNullOrWhiteSpace(currentNotesDirectory)
        ? CollectionPath
        : currentNotesDirectory;

    public string? CurrentNotePath => currentNotePath;

    public bool IsLoadingNotesDirectory
    {
        get => isLoadingNotesDirectory;
        private set
        {
            if (SetProperty(ref isLoadingNotesDirectory, value))
            {
                OnPropertyChanged(nameof(IsLoadingCollection));
            }
        }
    }

    public bool IsLoadingCollection => isLoadingNotesDirectory;

    public void Initialize()
    {
        string defaultCollectionPath = collectionPathResolver.ResolveDefaultCollectionPath();
        NotesDirectory = defaultCollectionPath;

        if (Directory.Exists(defaultCollectionPath) && collectionPathResolver.IsCollectionPath(defaultCollectionPath))
        {
            LoadNotesDirectory(defaultCollectionPath, clearEditor: false);
        }

        ShowDashboard();
    }

    public void LoadNotesDirectory(string directoryPath, bool clearEditor = true)
    {
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath.Trim()));
        }
        catch (Exception ex)
        {
            RaiseMessage("Invalid Collection", $"The collection path is invalid: {ex.Message}");
            return;
        }

        if (!collectionPathResolver.IsCollectionPath(fullPath))
        {
            RaiseMessage(
                "Invalid Collection",
                $"Only collection folders ending with \"{collectionPathResolver.CollectionDirectoryExtension}\" can be opened.");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            RaiseMessage("Collection Not Found", $"The collection folder does not exist:{Environment.NewLine}{fullPath}");
            return;
        }

        bool isSwitchingCollection =
            !string.IsNullOrWhiteSpace(currentNotesDirectory)
            && !string.Equals(currentNotesDirectory, fullPath, StringComparison.OrdinalIgnoreCase);

        if (isSwitchingCollection)
        {
            SaveCurrentNoteFromEditor();
            noteService.CloseCurrentCollection();
        }

        currentNotesDirectory = fullPath;
        NotesDirectory = currentNotesDirectory;
        OnPropertyChanged(nameof(OpenCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionPath));
        ClearCurrentNoteState();
        RefreshNotesTree(preserveExpansion: true);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (clearEditor)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public PreparedNote? PrepareNoteForOpen(string notePath)
    {
        if (string.Equals(currentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SaveCurrentNoteFromEditor();
        string fullPath = Path.GetFullPath(notePath);
        string content = noteService.ReadNoteFile(fullPath);
        TreeItemRow? noteItem = string.IsNullOrWhiteSpace(currentNotesDirectory)
            ? null
            : noteService.FindNoteByPath(currentNotesDirectory, fullPath);

        KnowledgeOpenedNoteReadModel openedNote = new(
            string.IsNullOrWhiteSpace(currentNotesDirectory)
                ? fullPath
                : Path.GetRelativePath(currentNotesDirectory, fullPath),
            noteItem?.Title ?? Path.GetFileNameWithoutExtension(fullPath),
            content,
            noteItem?.Id);

        return new PreparedNote(
            fullPath,
            openedNote.Title,
            openedNote.Content,
            openedNote.ItemId);
    }

    public void LoadCollection(string directoryPath, bool clearEditor = true)
    {
        LoadNotesDirectory(directoryPath, clearEditor);
    }

    public void CompleteOpenNote(PreparedNote note)
    {
        currentNotePath = note.NotePath;
        currentNoteItemId = note.ItemId;
        currentNoteTitle = note.Title;
        lastSavedContent = note.Content;
        OnPropertyChanged(nameof(CurrentNotePath));
    }

    public void ClearCurrentNote()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        ClearEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SaveCurrentNoteFromEditor()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        SaveNoteIfChanged(GetCurrentEditorContent?.Invoke() ?? string.Empty);
    }

    public void SaveNoteIfChanged(string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return;
        }

        noteService.SaveNote(currentNotePath, content);
        lastSavedContent = content;
    }

    public void SelectTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null || node.IsPlaceholder)
        {
            ClearCurrentNote();
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.NotePath))
        {
            currentNoteItemId = node.Id;
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(node.NotePath, SelectInTree: false));
            return;
        }
    }

    public bool CanRenameNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public bool CanDeleteNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public string GetEditableName(NoteTreeNodeViewModel node)
    {
        return node.Title;
    }

    public string GetNodeKind(NoteTreeNodeViewModel node)
    {
        return node.Kind is KnowledgeItemKind.Note or KnowledgeItemKind.PdfDocument ? "note" : "folder";
    }

    public void RenameNode(NoteTreeNodeViewModel node, string newName)
    {
        EnsureCanModifyNode(node, "renamed");

        noteService.RenameItem(currentNotesDirectory, node.Id, newName);
        node.UpdateTitle(newName);

        if (currentNoteItemId == node.Id)
        {
            currentNoteTitle = newName;
            CurrentNoteTitleChanged?.Invoke(this, currentNoteTitle);
        }

        UpdateRecentNoteTitle(node.NotePath, newName);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteNode(NoteTreeNodeViewModel node)
    {
        EnsureCanModifyNode(node, "deleted");
        SaveCurrentNoteFromEditor();

        string? currentNoteFullPath = string.IsNullOrWhiteSpace(currentNotePath)
            ? null
            : Path.GetFullPath(currentNotePath);

        IReadOnlyList<string> deletedNotePaths = noteService.DeleteItem(currentNotesDirectory, node.Id);
        bool deletesCurrentNote = currentNoteFullPath is not null
            && deletedNotePaths.Any(path => string.Equals(
                Path.GetFullPath(path),
                currentNoteFullPath,
                StringComparison.OrdinalIgnoreCase));

        RemoveLoadedTreeNode(node);
        foreach (string notePath in deletedNotePaths)
        {
            RemoveRecentNote(notePath);
        }
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (deletesCurrentNote)
        {
            ClearCurrentNoteState();
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<string> ImportDocuments(IEnumerable<string> sourcePaths)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        SaveCurrentNoteFromEditor();
        IReadOnlyList<string> importedPaths = noteService.ImportNoteFiles(currentNotesDirectory, sourcePaths);

        if (importedPaths.Count == 0)
        {
            return importedPaths;
        }

        foreach (string importedPath in importedPaths)
        {
            TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, importedPath);
            AddLoadedTreeNode(item);
            AddOrUpdateRecentNote(item, importedPath);
        }

        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(importedPaths[0], SelectInTree: true));

        return importedPaths;
    }

    public bool CanCreateFolderInNode(NoteTreeNodeViewModel? node)
    {
        return !string.IsNullOrWhiteSpace(currentNotesDirectory)
            && (node is null || node.CanContainChildren);
    }

    public string CreateFolder(NoteTreeNodeViewModel? node, string folderName)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        long folderId = noteService.CreateFolder(
            currentNotesDirectory,
            GetFolderCreationParentId(node),
            folderName);

        AddLoadedTreeNode(noteService.GetItem(currentNotesDirectory, folderId));
        return folderId.ToString();
    }

    public bool CanMoveNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
    }

    public void MoveTreeNode(
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        NoteTreeNodeViewModel? node = FindTreeNode(itemId);
        long? oldParentId = node?.ParentId;

        PrepareLoadedParentForMove(newParentId);

        noteService.MoveItem(
            currentNotesDirectory,
            itemId,
            newParentId,
            previousSiblingId,
            nextSiblingId);

        if (node is not null)
        {
            MoveLoadedTreeNode(node, oldParentId, newParentId, previousSiblingId, nextSiblingId);
        }
        else
        {
            RefreshNotesTree(preserveExpansion: true);
        }

    }

    public string CreateExtractNote(string? sourcePath, string selectedContent, string selectedPlainText)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The extract source is not the current note file.");
        }

        long? parentId = currentNoteItemId;
        NoteTreeNodeViewModel? parentNode = parentId.HasValue
            ? FindTreeNode(parentId.Value)
            : null;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Extract";
        string content = string.IsNullOrWhiteSpace(selectedContent)
            ? BlockSuiteDocumentContent.FromPlainText(selectedPlainText, title)
            : selectedContent;
        CreatedNote note = noteService.CreateExtractNote(
            currentNotesDirectory,
            parentId,
            sourcePath,
            title,
            content);

        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return note.FileRelPath;
    }

    public MentionPreview ReadMentionPreview(string relativePath, int maxCharacters)
    {
        return noteService.ReadMentionPreview(currentNotesDirectory, relativePath, maxCharacters);
    }

    public string ResolveMentionPath(string relativePath)
    {
        return noteService.ResolveMentionPath(currentNotesDirectory, relativePath);
    }

    public LinkedNote CreateLinkedNote(string sourcePath, string viewMode)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The linked note source is not the current note file.");
        }

        NoteTreeNodeViewModel? parentNode = currentNoteItemId.HasValue
            ? FindTreeNode(currentNoteItemId.Value)
            : null;

        long? parentId = parentNode is { CanContainChildren: true }
            ? parentNode.Id
            : parentNode?.ParentId;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Untitled";
        string documentContent = BlockSuiteDocumentContent.FromPlainText(string.Empty, title);
        CreatedNote note = noteService.CreateNote(
            currentNotesDirectory,
            parentId,
            title,
            documentContent);

        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);

        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return new LinkedNote(note.NotePath, note.FileRelPath, title, documentContent);
    }

    public void SaveMentionSource(string sourcePath, string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The mention source is not the current note file.");
        }

        SaveNoteIfChanged(content);
    }

    public IReadOnlyList<MentionTarget> ListMentionTargets()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return [];
        }

        try
        {
            return noteService.ListMentionTargets(currentNotesDirectory, CreateRecentMentionRanks());
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            RaiseMessage("Mention Index Failed", $"Mentions could not be indexed: {ex.Message}");
            return [];
        }
    }

    public IReadOnlyList<MentionTarget> SearchMentionTargets(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return [];
        }

        try
        {
            return noteService.SearchMentionTargets(currentNotesDirectory, query, limit);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            RaiseMessage("Mention Search Failed", $"Mentions could not be searched: {ex.Message}");
            return [];
        }
    }

    private void ShowDashboard()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new OverlayRequestedEventArgs(OverlayViewKind.Dashboard));
    }

    private void ShowAllNotes()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new OverlayRequestedEventArgs(OverlayViewKind.AllNotes));
    }

    private void ShowDecks()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new OverlayRequestedEventArgs(OverlayViewKind.Decks));
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            return;
        }

        CreatedNote note = noteService.CreateNote(
            currentNotesDirectory,
            parentId: null,
            "Untitled",
            BlockSuiteDocumentContent.FromPlainText(string.Empty, "Untitled"));
        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(note.NotePath, SelectInTree: true));
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        if (parameter is string notePath && !string.IsNullOrWhiteSpace(notePath))
        {
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(notePath, SelectInTree: true));
        }
    }

    private bool CanModifyNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
    }

    private static long? GetFolderCreationParentId(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.CanContainChildren)
        {
            return node.Id;
        }

        return node.ParentId;
    }

    private void EnsureCanModifyNode(NoteTreeNodeViewModel node, string operation)
    {
        if (!CanModifyNode(node))
        {
            throw new InvalidOperationException($"This item cannot be {operation}.");
        }
    }

    private void RefreshNotesTree(bool preserveExpansion = false)
    {
        HashSet<long> expandedItemIds = preserveExpansion
            ? CaptureExpandedItemIds()
            : [];

        IsLoadingNotesDirectory = true;

        try
        {
            NoteRoots.Clear();

            if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
            {
                return;
            }

            List<string> noteFiles = EnumerateNoteFiles(currentNotesDirectory).ToList();

            PopulateRecentNotes(noteFiles);

            foreach (TreeItemRow item in noteService.LoadRootItems(currentNotesDirectory))
            {
                NoteRoots.Add(CreateTreeNode(item));
            }

            if (expandedItemIds.Count > 0)
            {
                RestoreExpandedNodes(NoteRoots, expandedItemIds);
            }
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            NoteRoots.Clear();
            RecentNotes.Clear();
            RaiseMessage("Collection Load Failed", $"The collection could not be read: {ex.Message}");
        }
        finally
        {
            IsLoadingNotesDirectory = false;
        }
    }

    private void AddOrUpdateRecentNote(TreeItemRow? item, string? notePath)
    {
        if (item is null
            || item.Kind is not (TreeItemKind.Note or TreeItemKind.PdfDocument)
            || string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        RemoveRecentNote(notePath);
        RecentNotes.Insert(0, new RecentNoteViewModel(item.Title, Path.GetFullPath(notePath)));

        while (RecentNotes.Count > 5)
        {
            RecentNotes.RemoveAt(RecentNotes.Count - 1);
        }
    }

    private void UpdateRecentNoteTitle(string? notePath, string title)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = 0; index < RecentNotes.Count; index++)
        {
            RecentNoteViewModel recentNote = RecentNotes[index];
            if (string.Equals(
                    Path.GetFullPath(recentNote.NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes[index] = recentNote with { DisplayName = title };
                return;
            }
        }
    }

    private void RemoveRecentNote(string? notePath)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = RecentNotes.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                    Path.GetFullPath(RecentNotes[index].NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes.RemoveAt(index);
            }
        }
    }

    private void PopulateRecentNotes(IEnumerable<string> noteFiles)
    {
        RecentNotes.Clear();

        foreach (string filePath in noteFiles
                     .OrderByDescending(File.GetLastWriteTime)
                     .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            TreeItemRow? noteItem = TryFindNoteMetadata(filePath);
            if (noteItem is null)
            {
                continue;
            }

            RecentNotes.Add(new RecentNoteViewModel(
                noteItem.Title,
                filePath));

            if (RecentNotes.Count >= 5)
            {
                break;
            }
        }
    }

    private TreeItemRow? TryFindNoteMetadata(string notePath)
    {
        try
        {
            return noteService.FindNoteByPath(currentNotesDirectory, notePath);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            return null;
        }
    }

    public void LoadChildren(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory)
            || node.IsPlaceholder
            || !node.CanContainChildren
            || node.AreChildrenLoaded)
        {
            return;
        }

        node.Children.Clear();

        foreach (TreeItemRow child in noteService.LoadChildren(currentNotesDirectory, node.Id))
        {
            node.Children.Add(CreateTreeNode(child));
        }

        node.MarkChildrenLoaded();
    }

    private NoteTreeNodeViewModel CreateTreeNode(TreeItemRow item)
    {
        KnowledgeTreeItemRow knowledgeItem = ToKnowledgeTreeItemRow(item);
        KnowledgeItemKind itemKind = ToKnowledgeItemKind(knowledgeItem.Kind);
        string? notePath = itemKind is KnowledgeItemKind.Note or KnowledgeItemKind.PdfDocument
            && !string.IsNullOrWhiteSpace(knowledgeItem.FileRelPath)
            ? Path.Combine(currentNotesDirectory, knowledgeItem.FileRelPath.Replace('/', Path.DirectorySeparatorChar))
            : null;

        NoteTreeNodeViewModel node = new(
            knowledgeItem.Id,
            knowledgeItem.ParentId,
            itemKind,
            knowledgeItem.Title,
            knowledgeItem.FileRelPath,
            notePath,
            knowledgeItem.ChildCount)
        {
            IsExpanded = false,
        };

        if (item.ChildCount > 0 && node.CanContainChildren)
        {
            node.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }

        return node;
    }

    private static KnowledgeTreeItemRow ToKnowledgeTreeItemRow(TreeItemRow item)
    {
        return new KnowledgeTreeItemRow(
            item.Id,
            item.Uid,
            item.ParentId,
            item.Kind switch
            {
                TreeItemKind.Root => KnowledgeTreeItemKind.Root,
                TreeItemKind.Trash => KnowledgeTreeItemKind.Trash,
                TreeItemKind.Note => KnowledgeTreeItemKind.Note,
                TreeItemKind.PdfDocument => KnowledgeTreeItemKind.PdfDocument,
                _ => KnowledgeTreeItemKind.Folder,
            },
            item.Title,
            item.Position,
            item.FileRelPath,
            item.ChildCount,
            item.IsDeleted,
            item.CreatedAt,
            item.UpdatedAt,
            item.FileSize,
            item.FileMtimeUtc,
            item.ContentHash);
    }

    private static KnowledgeItemKind ToKnowledgeItemKind(KnowledgeTreeItemKind kind)
    {
        return kind switch
        {
            KnowledgeTreeItemKind.Root => KnowledgeItemKind.Root,
            KnowledgeTreeItemKind.Trash => KnowledgeItemKind.Trash,
            KnowledgeTreeItemKind.Note => KnowledgeItemKind.Note,
            KnowledgeTreeItemKind.PdfDocument => KnowledgeItemKind.PdfDocument,
            _ => KnowledgeItemKind.Folder,
        };
    }

    private void AddLoadedTreeNode(TreeItemRow? item)
    {
        if (item is null)
        {
            RefreshNotesTree(preserveExpansion: true);
            return;
        }

        NoteTreeNodeViewModel node = CreateTreeNode(item);
        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindLoadedSiblingCollection(item.ParentId);

        if (siblings is not null)
        {
            InsertNodeBySiblings(siblings, node, previousSiblingId: null, nextSiblingId: null);
        }

        UpdateParentChildCount(item.ParentId, delta: 1);
    }

    private void RemoveLoadedTreeNode(NoteTreeNodeViewModel node)
    {
        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindOwningCollection(node);

        if (siblings is not null)
        {
            siblings.Remove(node);
        }

        UpdateParentChildCount(node.ParentId, delta: -1);
    }

    private void MoveLoadedTreeNode(
        NoteTreeNodeViewModel node,
        long? oldParentId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ObservableCollection<NoteTreeNodeViewModel>? oldSiblings = FindOwningCollection(node);

        if (oldSiblings is not null)
        {
            oldSiblings.Remove(node);
        }

        node.UpdateParent(newParentId);

        ObservableCollection<NoteTreeNodeViewModel>? newSiblings = FindLoadedSiblingCollection(newParentId);
        if (newSiblings is not null)
        {
            RemoveNodeById(newSiblings, node.Id);
            InsertNodeBySiblings(newSiblings, node, previousSiblingId, nextSiblingId);
        }

        if (oldParentId != newParentId)
        {
            UpdateParentChildCount(oldParentId, delta: -1);
            UpdateParentChildCount(newParentId, delta: 1);
        }
    }

    private void PrepareLoadedParentForMove(long? parentId)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        if (parent is not { CanContainChildren: true })
        {
            return;
        }

        if (!parent.AreChildrenLoaded)
        {
            LoadChildren(parent);
        }

        parent.IsExpanded = true;
    }

    private void InsertNodeBySiblings(
        ObservableCollection<NoteTreeNodeViewModel> siblings,
        NoteTreeNodeViewModel node,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        RemovePlaceholders(siblings);

        int insertIndex = siblings.Count;

        if (previousSiblingId.HasValue)
        {
            int previousIndex = IndexOfNode(siblings, previousSiblingId.Value);
            if (previousIndex >= 0)
            {
                insertIndex = previousIndex + 1;
            }
        }
        else if (nextSiblingId.HasValue)
        {
            int nextIndex = IndexOfNode(siblings, nextSiblingId.Value);
            if (nextIndex >= 0)
            {
                insertIndex = nextIndex;
            }
        }

        siblings.Insert(Math.Clamp(insertIndex, 0, siblings.Count), node);
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindLoadedSiblingCollection(long? parentId)
    {
        if (parentId is null)
        {
            return NoteRoots;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        return parent is { AreChildrenLoaded: true } ? parent.Children : null;
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(NoteTreeNodeViewModel target)
    {
        return FindOwningCollection(NoteRoots, target);
    }

    private static ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(
        ObservableCollection<NoteTreeNodeViewModel> nodes,
        NoteTreeNodeViewModel target)
    {
        if (nodes.Contains(target))
        {
            return nodes;
        }

        foreach (NoteTreeNodeViewModel node in nodes)
        {
            ObservableCollection<NoteTreeNodeViewModel>? result = FindOwningCollection(node.Children, target);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static void RemoveNodeById(ObservableCollection<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            NoteTreeNodeViewModel node = nodes[index];
            if (!node.IsPlaceholder && node.Id == id)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    private NoteTreeNodeViewModel? FindTreeNode(long id)
    {
        return FindTreeNode(NoteRoots, id);
    }

    private static NoteTreeNodeViewModel? FindTreeNode(IEnumerable<NoteTreeNodeViewModel> nodes, long id)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (!node.IsPlaceholder && node.Id == id)
            {
                return node;
            }

            NoteTreeNodeViewModel? result = FindTreeNode(node.Children, id);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void UpdateParentChildCount(long? parentId, int delta)
    {
        if (parentId is null)
        {
            return;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        if (parent is null)
        {
            return;
        }

        int childCount = parent.AreChildrenLoaded
            ? parent.Children.Count(child => !child.IsPlaceholder)
            : parent.ChildCount + delta;
        parent.UpdateChildCount(childCount);

        if (parent.AreChildrenLoaded || !parent.CanContainChildren)
        {
            return;
        }

        if (parent.ChildCount > 0 && parent.Children.Count == 0)
        {
            parent.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }
        else if (parent.ChildCount == 0)
        {
            parent.Children.Clear();
            parent.AreChildrenLoaded = true;
        }
    }

    private static int IndexOfNode(IReadOnlyList<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (!nodes[index].IsPlaceholder && nodes[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void RemovePlaceholders(ObservableCollection<NoteTreeNodeViewModel> nodes)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            if (nodes[index].IsPlaceholder)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    private HashSet<long> CaptureExpandedItemIds()
    {
        HashSet<long> expandedItemIds = [];
        CaptureExpandedItemIds(NoteRoots, expandedItemIds);
        return expandedItemIds;
    }

    private static void CaptureExpandedItemIds(
        IEnumerable<NoteTreeNodeViewModel> nodes,
        ISet<long> expandedItemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (node.IsPlaceholder)
            {
                continue;
            }

            if (node.IsExpanded)
            {
                expandedItemIds.Add(node.Id);
            }

            CaptureExpandedItemIds(node.Children, expandedItemIds);
        }
    }

    private void RestoreExpandedNodes(
        IEnumerable<NoteTreeNodeViewModel> nodes,
        IReadOnlySet<long> expandedItemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (node.IsPlaceholder || !expandedItemIds.Contains(node.Id))
            {
                continue;
            }

            LoadChildren(node);
            node.IsExpanded = true;
            RestoreExpandedNodes(node.Children, expandedItemIds);
        }
    }

    private IReadOnlyDictionary<string, int> CreateRecentMentionRanks()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return EnumerateNoteFiles(currentNotesDirectory)
            .OrderByDescending(File.GetLastWriteTime)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select((path, index) => new
            {
                Path = Path.GetFullPath(path),
                Rank = index,
            })
            .ToDictionary(item => item.Path, item => item.Rank, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateNoteFiles(string root)
    {
        return Directory
            .EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void ClearCurrentNoteState()
    {
        currentNotePath = null;
        currentNoteItemId = null;
        currentNoteTitle = string.Empty;
        lastSavedContent = string.Empty;
        OnPropertyChanged(nameof(CurrentNotePath));
    }

    private void RaiseMessage(string title, string message)
    {
        UserMessageRequested?.Invoke(this, new UserMessageRequestedEventArgs(title, message));
    }

    private void RunUserAction(string title, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            RaiseMessage(title, ex.Message);
        }
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or NotSupportedException;
    }

    public void Dispose()
    {
        noteService.Dispose();
    }
}

