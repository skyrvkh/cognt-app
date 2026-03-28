using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CogntApp.Models.Markdown;
using CogntApp.Models.Navigation;
using CogntApp.Services;

namespace CogntApp.ViewModels;

public sealed record NoteGroupResult(string TargetNotePath);

public sealed class MainViewModel : ObservableObject
{
    private const string CollectionDirectoryExtension = ".cognt";

    private readonly IMarkdownNoteService noteService;
    private string notesDirectory = string.Empty;
    private string currentNotesDirectory = string.Empty;
    private string? currentNotePath;
    private string lastSavedContent = string.Empty;
    private bool isLoadingNotesDirectory;

    public MainViewModel(IMarkdownNoteService noteService)
    {
        this.noteService = noteService;

        LoadDirectoryCommand = new RelayCommand(() => LoadNotesDirectory(NotesDirectory));
        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowDecksCommand = new RelayCommand(ShowDecks);
        AddNoteCommand = new RelayCommand(AddNote);
        OpenNoteCommand = new RelayCommand(OpenNoteFromCommand);
    }

    public event EventHandler<OverlayRequestedEventArgs>? OverlayRequested;

    public event EventHandler? ClearEditorRequested;

    public event EventHandler<OpenNoteRequestedEventArgs>? OpenNoteRequested;

    public event EventHandler<NoteSelectionRequestedEventArgs>? NoteSelectionRequested;

    public event EventHandler? MentionTargetsChanged;

    public event EventHandler<UserMessageRequestedEventArgs>? UserMessageRequested;

    public Func<string>? GetCurrentEditorContent { get; set; }

    public ObservableCollection<NoteTreeNodeViewModel> NoteRoots { get; } = [];

    public ObservableCollection<RecentNoteViewModel> RecentNotes { get; } = [];

    public ICommand LoadDirectoryCommand { get; }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowDecksCommand { get; }

    public ICommand AddNoteCommand { get; }

    public ICommand OpenNoteCommand { get; }

    public string NotesDirectory
    {
        get => notesDirectory;
        set => SetProperty(ref notesDirectory, value);
    }

    public string CurrentNotesDirectory => currentNotesDirectory;

    public string? CurrentNotePath => currentNotePath;

    public bool IsLoadingNotesDirectory
    {
        get => isLoadingNotesDirectory;
        private set => SetProperty(ref isLoadingNotesDirectory, value);
    }

    public void Initialize()
    {
        string defaultCollectionPath = ResolveDefaultNotesDirectory();
        NotesDirectory = defaultCollectionPath;

        if (Directory.Exists(defaultCollectionPath) && IsCognCollectionPath(defaultCollectionPath))
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

        if (!IsCognCollectionPath(fullPath))
        {
            RaiseMessage(
                "Invalid Collection",
                $"Only collection folders ending with \"{CollectionDirectoryExtension}\" can be opened.");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            RaiseMessage("Collection Not Found", $"The collection folder does not exist:{Environment.NewLine}{fullPath}");
            return;
        }

        currentNotesDirectory = fullPath;
        NotesDirectory = currentNotesDirectory;
        ClearCurrentNoteState();
        RefreshNotesTree();
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
        string content = File.ReadAllText(fullPath);

        return new PreparedNote(fullPath, Path.GetFileName(fullPath), content);
    }

    public void CompleteOpenNote(PreparedNote note)
    {
        currentNotePath = note.NotePath;
        lastSavedContent = note.Content;
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
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
        if (node?.NotePath is string notePath)
        {
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(notePath, SelectInTree: false));
            return;
        }

        ClearCurrentNote();
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
        return node.IsMarkdownFile && !string.IsNullOrWhiteSpace(node.NotePath)
            ? Path.GetFileNameWithoutExtension(node.NotePath)
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(node.Path));
    }

    public string GetNodeKind(NoteTreeNodeViewModel node)
    {
        return node.IsMarkdownFile ? "Markdown file" : "folder";
    }

    public void RenameNode(NoteTreeNodeViewModel node, string newName)
    {
        EnsureCanModifyNode(node, "renamed");
        SaveCurrentNoteFromEditor();

        string? noteToOpen = node.IsMarkdownFile
            ? RenameMarkdownNode(node, newName)
            : RenameFolderNode(node, newName);

        RefreshNotesTree();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (!string.IsNullOrWhiteSpace(noteToOpen))
        {
            ClearCurrentNoteState();
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(noteToOpen, SelectInTree: true));
        }
        else if (!string.IsNullOrWhiteSpace(currentNotePath))
        {
            NoteSelectionRequested?.Invoke(this, new NoteSelectionRequestedEventArgs(currentNotePath));
        }
    }

    public void DeleteNode(NoteTreeNodeViewModel node)
    {
        EnsureCanModifyNode(node, "deleted");
        SaveCurrentNoteFromEditor();

        bool deletesCurrentNote = CurrentNoteIsInsideNode(node);

        if (node.IsMarkdownFile)
        {
            noteService.DeleteMarkdownFile(currentNotesDirectory, node.NotePath ?? node.Path);
        }
        else
        {
            noteService.DeleteFolder(currentNotesDirectory, node.Path);
        }

        RefreshNotesTree();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (deletesCurrentNote)
        {
            ClearCurrentNoteState();
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (!string.IsNullOrWhiteSpace(currentNotePath))
        {
            NoteSelectionRequested?.Invoke(this, new NoteSelectionRequestedEventArgs(currentNotePath));
        }
    }

    public MarkdownNoteGroupResult GroupNoteWithTarget(string droppedNotePath, string targetNotePath)
    {
        SaveCurrentNoteFromEditor();
        MarkdownNoteGroupResult result = noteService.GroupNoteWithTarget(
            currentNotesDirectory,
            droppedNotePath,
            targetNotePath);

        RefreshNotesTree();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public string CreateExtractFile(string? sourcePath, string selectedMarkdown)
    {
        string createdPath = noteService.CreateExtractFile(new CreateExtractFileRequest(
            currentNotesDirectory,
            currentNotePath ?? string.Empty,
            sourcePath ?? string.Empty,
            selectedMarkdown));

        RefreshNotesTree();
        if (!string.IsNullOrWhiteSpace(currentNotePath))
        {
            NoteSelectionRequested?.Invoke(this, new NoteSelectionRequestedEventArgs(currentNotePath));
        }

        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return createdPath;
    }

    public MarkdownMentionPreview ReadMentionPreview(string relativePath, int maxCharacters)
    {
        return noteService.ReadMentionPreview(currentNotesDirectory, relativePath, maxCharacters);
    }

    public string ResolveMentionPath(string relativePath)
    {
        return noteService.ResolveMentionPath(currentNotesDirectory, relativePath);
    }

    public void SaveMentionSource(string sourcePath, string markdown)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The mention source is not the current Markdown file.");
        }

        SaveNoteIfChanged(markdown);
    }

    public IReadOnlyList<MarkdownMentionTarget> ListMentionTargets()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return [];
        }

        return noteService.ListMentionTargets(currentNotesDirectory, CreateRecentMentionRanks());
    }

    private void ShowDashboard()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new OverlayRequestedEventArgs(OverlayViewKind.Dashboard));
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

        string notePath = noteService.CreateUniqueNotePath(currentNotesDirectory);
        noteService.SaveNote(notePath, "# Untitled" + Environment.NewLine);
        LoadNotesDirectory(currentNotesDirectory, clearEditor: false);
        OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(notePath, SelectInTree: true));
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        if (parameter is string notePath && !string.IsNullOrWhiteSpace(notePath))
        {
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(notePath, SelectInTree: true));
        }
    }

    private string RenameMarkdownNode(NoteTreeNodeViewModel node, string newName)
    {
        string sourcePath = node.NotePath ?? node.Path;

        if (TryGetPrimaryNoteFolder(sourcePath, out string? primaryFolderPath))
        {
            string folderName = GetMarkdownBaseName(newName);
            string renamedFolderPath = noteService.RenameFolder(
                currentNotesDirectory,
                primaryFolderPath,
                folderName);
            string movedPrimaryPath = Path.Combine(renamedFolderPath, Path.GetFileName(sourcePath));
            string renamedNotePath = noteService.RenameMarkdownFile(
                currentNotesDirectory,
                movedPrimaryPath,
                newName);

            return MapCurrentPathAfterCollectionRename(
                primaryFolderPath,
                renamedFolderPath,
                sourcePath,
                renamedNotePath)
                ?? renamedNotePath;
        }

        return noteService.RenameMarkdownFile(currentNotesDirectory, sourcePath, newName);
    }

    private string? RenameFolderNode(NoteTreeNodeViewModel node, string newName)
    {
        string sourceFolderPath = node.Path;
        string? primaryNotePath = node.NotePath;
        string renamedFolderPath = noteService.RenameFolder(currentNotesDirectory, sourceFolderPath, newName);

        if (!string.IsNullOrWhiteSpace(primaryNotePath))
        {
            string movedPrimaryPath = Path.Combine(renamedFolderPath, Path.GetFileName(primaryNotePath));
            string renamedPrimaryPath = noteService.RenameMarkdownFile(
                currentNotesDirectory,
                movedPrimaryPath,
                $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(renamedFolderPath))}.md");

            return MapCurrentPathAfterCollectionRename(
                sourceFolderPath,
                renamedFolderPath,
                primaryNotePath,
                renamedPrimaryPath)
                ?? renamedPrimaryPath;
        }

        return MapCurrentPathAfterFolderMove(sourceFolderPath, renamedFolderPath, currentNotePath);
    }

    private bool CanModifyNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !IsOpenRootFolder(node)
            && (node.IsMarkdownFile || Directory.Exists(node.Path));
    }

    private void EnsureCanModifyNode(NoteTreeNodeViewModel node, string operation)
    {
        if (IsOpenRootFolder(node))
        {
            throw new InvalidOperationException($"The open notes root cannot be {operation}.");
        }

        if (!CanModifyNode(node))
        {
            throw new InvalidOperationException($"This item cannot be {operation}.");
        }
    }

    private bool IsOpenRootFolder(NoteTreeNodeViewModel node)
    {
        return !node.IsMarkdownFile
            && !string.IsNullOrWhiteSpace(currentNotesDirectory)
            && string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(node.Path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentNotesDirectory)),
                StringComparison.OrdinalIgnoreCase);
    }

    private bool CurrentNoteIsInsideNode(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return false;
        }

        return node.IsMarkdownFile
            ? PathsEqual(currentNotePath, node.NotePath ?? node.Path)
            : IsPathInsideFolder(currentNotePath, node.Path);
    }

    private bool TryGetPrimaryNoteFolder(string notePath, out string folderPath)
    {
        folderPath = string.Empty;
        string? parentFolder = Path.GetDirectoryName(notePath);
        if (string.IsNullOrWhiteSpace(parentFolder)
            || string.IsNullOrWhiteSpace(currentNotesDirectory)
            || PathsEqual(parentFolder, currentNotesDirectory))
        {
            return false;
        }

        string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(parentFolder));
        string noteName = Path.GetFileNameWithoutExtension(notePath);
        if (!string.Equals(folderName, noteName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        folderPath = parentFolder;
        return true;
    }

    private static string GetMarkdownBaseName(string newName)
    {
        string trimmedName = newName.Trim();
        return string.Equals(Path.GetExtension(trimmedName), ".md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(trimmedName)
            : trimmedName;
    }

    private string? MapCurrentPathAfterCollectionRename(
        string sourceFolderPath,
        string renamedFolderPath,
        string sourcePrimaryNotePath,
        string renamedPrimaryNotePath)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return null;
        }

        if (PathsEqual(currentNotePath, sourcePrimaryNotePath))
        {
            return renamedPrimaryNotePath;
        }

        return MapCurrentPathAfterFolderMove(sourceFolderPath, renamedFolderPath, currentNotePath);
    }

    private static string? MapCurrentPathAfterFolderMove(
        string sourceFolderPath,
        string renamedFolderPath,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsPathInsideFolder(path, sourceFolderPath))
        {
            return null;
        }

        return Path.Combine(
            renamedFolderPath,
            Path.GetRelativePath(sourceFolderPath, path));
    }

    private static bool IsPathInsideFolder(string path, string folderPath)
    {
        string fullPath = Path.GetFullPath(path);
        string fullFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        string folderWithSeparator = $"{fullFolder}{Path.DirectorySeparatorChar}";

        return fullPath.StartsWith(folderWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshNotesTree()
    {
        IsLoadingNotesDirectory = true;

        try
        {
            NoteRoots.Clear();

            if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
            {
                return;
            }

            List<string> noteFiles = Directory.EnumerateFiles(currentNotesDirectory, "*.md", SearchOption.AllDirectories)
                .ToList();

            PopulateRecentNotes(noteFiles);

            NoteTreeNodeViewModel rootNode = new(GetDirectoryName(currentNotesDirectory), currentNotesDirectory, isMarkdownFile: false)
            {
                IsExpanded = true,
            };

            NoteRoots.Add(rootNode);

            Dictionary<string, NoteTreeNodeViewModel> folderItems = new(StringComparer.OrdinalIgnoreCase)
            {
                [currentNotesDirectory] = rootNode,
            };

            foreach (string filePath in noteFiles.OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
            {
                NoteTreeNodeViewModel fileParent = GetOrCreateParentFolderNode(filePath, rootNode, folderItems);
                NoteTreeNodeViewModel item = CreateNoteFileNode(filePath);

                fileParent.Children.Add(item);

                if (TryAssignPrimaryFolderNote(fileParent, filePath))
                {
                    fileParent.IsExpanded = true;
                }
            }
        }
        finally
        {
            IsLoadingNotesDirectory = false;
        }
    }

    private void PopulateRecentNotes(IEnumerable<string> noteFiles)
    {
        RecentNotes.Clear();

        foreach (string filePath in noteFiles
                     .OrderByDescending(File.GetLastWriteTime)
                     .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                     .Take(5))
        {
            RecentNotes.Add(new RecentNoteViewModel(
                Path.GetFileNameWithoutExtension(filePath),
                filePath));
        }
    }

    private NoteTreeNodeViewModel GetOrCreateParentFolderNode(
        string notePath,
        NoteTreeNodeViewModel rootNode,
        IDictionary<string, NoteTreeNodeViewModel> folderItems)
    {
        string relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(currentNotesDirectory, notePath))
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relativeDirectory))
        {
            return rootNode;
        }

        NoteTreeNodeViewModel parentItem = rootNode;
        string currentDirectory = currentNotesDirectory;

        foreach (string segment in relativeDirectory.Split(
                     Path.DirectorySeparatorChar,
                     Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            currentDirectory = Path.Combine(currentDirectory, segment);
            if (!folderItems.TryGetValue(currentDirectory, out NoteTreeNodeViewModel? folderItem))
            {
                folderItem = new NoteTreeNodeViewModel(segment, currentDirectory, isMarkdownFile: false)
                {
                    IsExpanded = true,
                };
                folderItems[currentDirectory] = folderItem;
                parentItem.Children.Add(folderItem);
            }

            parentItem = folderItem;
        }

        return parentItem;
    }

    private static NoteTreeNodeViewModel CreateNoteFileNode(string notePath)
    {
        return new NoteTreeNodeViewModel(Path.GetFileName(notePath), notePath, isMarkdownFile: true)
        {
            NotePath = notePath,
        };
    }

    private static bool TryAssignPrimaryFolderNote(NoteTreeNodeViewModel folderItem, string notePath)
    {
        if (folderItem.IsMarkdownFile)
        {
            return false;
        }

        string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderItem.Path));
        string noteName = Path.GetFileNameWithoutExtension(notePath);
        if (!string.Equals(folderName, noteName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        folderItem.NotePath = notePath;
        return true;
    }

    private IReadOnlyDictionary<string, int> CreateRecentMentionRanks()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory
            .EnumerateFiles(currentNotesDirectory, "*.md", SearchOption.AllDirectories)
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

    private void ClearCurrentNoteState()
    {
        currentNotePath = null;
        lastSavedContent = string.Empty;
        OnPropertyChanged(nameof(CurrentNotePath));
    }

    private void RaiseMessage(string title, string message)
    {
        UserMessageRequested?.Invoke(this, new UserMessageRequestedEventArgs(title, message));
    }

    private static string ResolveDefaultNotesDirectory()
    {
        foreach (string root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(root);

            while (directory is not null)
            {
                string samplesCollectionPath = Path.Combine(directory.FullName, $"Samples{CollectionDirectoryExtension}");
                if (Directory.Exists(samplesCollectionPath))
                {
                    return samplesCollectionPath;
                }

                string? firstCollectionPath = FindFirstCollectionPath(directory.FullName);

                if (!string.IsNullOrWhiteSpace(firstCollectionPath))
                {
                    return firstCollectionPath;
                }

                directory = directory.Parent;
            }
        }

        return Path.Combine(Environment.CurrentDirectory, $"Samples{CollectionDirectoryExtension}");
    }

    private static string? FindFirstCollectionPath(string directoryPath)
    {
        try
        {
            return Directory
                .EnumerateDirectories(directoryPath, $"*{CollectionDirectoryExtension}", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsCognCollectionPath(string path)
    {
        string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

        return directoryName.EndsWith(CollectionDirectoryExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDirectoryName(string directoryPath)
    {
        return new DirectoryInfo(directoryPath).Name;
    }
}

