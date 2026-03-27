using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CogntApp.Services;

namespace CogntApp.ViewModels;

public sealed class MainViewModel : ObservableObject
{
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
        LoadNotesDirectory(ResolveDefaultNotesDirectory(), clearEditor: false);
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
            RaiseMessage("Invalid Directory", $"The notes directory path is invalid: {ex.Message}");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            RaiseMessage("Directory Not Found", $"The notes directory does not exist:{Environment.NewLine}{fullPath}");
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
                string samplesPath = Path.Combine(directory.FullName, "Samples");

                if (Directory.Exists(samplesPath))
                {
                    return samplesPath;
                }

                directory = directory.Parent;
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "Samples");
    }

    private static string GetDirectoryName(string directoryPath)
    {
        return new DirectoryInfo(directoryPath).Name;
    }
}

public sealed record PreparedNote(string NotePath, string Title, string Content);

public sealed record OpenNoteRequestedEventArgs(string NotePath, bool SelectInTree);

public sealed record NoteSelectionRequestedEventArgs(string NotePath);

public sealed record OverlayRequestedEventArgs(OverlayViewKind ViewKind);

public sealed record UserMessageRequestedEventArgs(string Title, string Message);

public enum OverlayViewKind
{
    Dashboard,
    Decks,
}
