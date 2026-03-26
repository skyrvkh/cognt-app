using System.IO;
using System.Windows;
using System.Windows.Controls;
using CogntApp.Services;
using CogntApp.Views;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DashboardView dashboardView = new();
    private readonly DecksView decksView = new();
    private readonly ReviewView reviewView = new();
    private readonly IMarkdownNoteService noteService;
    private string currentNotesDirectory = string.Empty;
    private string? currentNotePath;
    private string lastSavedContent = string.Empty;
    private bool loadingNotesDirectory;
    private int editorOpenRequestId;

    public MainWindow()
        : this(new MarkdownNoteService())
    {
    }

    internal MainWindow(IMarkdownNoteService noteService)
    {
        this.noteService = noteService;

        InitializeComponent();
        EditorViewControl.ContentChanged += EditorView_ContentChanged;
        EditorViewControl.CreateExtractRequested += EditorView_CreateExtractRequested;
        EditorViewControl.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        EditorViewControl.OpenMentionRequested += EditorView_OpenMentionRequested;
        LoadNotesDirectory(ResolveDefaultNotesDirectory());
        ShowDashboardView();
        _ = EditorViewControl.WarmUpAsync();
    }

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
        ShowDashboardView();
    }

    private async void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (loadingNotesDirectory)
        {
            return;
        }

        if (e.NewValue is not TreeViewItem selectedItem)
        {
            return;
        }

        if (selectedItem.Tag is string notePath)
        {
            if (string.Equals(currentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SaveCurrentNote();
            await ShowEditorViewAsync(notePath);
            return;
        }

        ShowDirectoryOverview();
    }

    private void LoadDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        LoadNotesDirectory(NotesDirectoryTextBox.Text);
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
        editorOpenRequestId++;
        currentNotePath = null;
        lastSavedContent = string.Empty;
        ShowOverlay(reviewView);
    }

    private void DecksButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
        editorOpenRequestId++;
        currentNotePath = null;
        lastSavedContent = string.Empty;
        ShowOverlay(decksView);
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            return;
        }

        string notePath = noteService.CreateUniqueNotePath(currentNotesDirectory);
        noteService.SaveNote(notePath, "# Untitled" + Environment.NewLine);
        LoadNotesDirectory(currentNotesDirectory);
        SelectNote(notePath);
    }

    private void SaveNoteButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
    }

    private void LoadNotesDirectory(string directoryPath)
    {
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath.Trim()));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The notes directory path is invalid: {ex.Message}", "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            MessageBox.Show($"The notes directory does not exist:{Environment.NewLine}{fullPath}", "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        currentNotesDirectory = fullPath;
        currentNotePath = null;
        lastSavedContent = string.Empty;
        NotesDirectoryTextBox.Text = currentNotesDirectory;
        NotesRoot.Header = GetDirectoryName(currentNotesDirectory);
        RefreshNotesTree();
        SendMentionTargets();

        ShowDirectoryOverview();
    }

    private async Task ShowEditorViewAsync(string notePath)
    {
        int requestId = ++editorOpenRequestId;
        string content = File.ReadAllText(notePath);

        try
        {
            await EditorViewControl.LoadNoteAsync(Path.GetFileName(notePath), content, notePath);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show($"The editor could not open this note: {ex.Message}", "Editor Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (requestId != editorOpenRequestId)
        {
            return;
        }

        currentNotePath = notePath;
        lastSavedContent = content;
        SendMentionTargets();
        HideOverlay();
    }

    private void ShowDashboardView()
    {
        editorOpenRequestId++;
        currentNotePath = null;
        lastSavedContent = string.Empty;
        ShowOverlay(dashboardView);
    }

    private void ShowDirectoryOverview()
    {
        editorOpenRequestId++;
        currentNotePath = null;
        lastSavedContent = string.Empty;
        int noteCount = NotesRoot.Items.Count;
        string title = GetDirectoryName(currentNotesDirectory);
        string content = noteCount == 1
            ? "1 Markdown file found."
            : $"{noteCount} Markdown files found.";

        ShowOverlay(new Border
        {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(215, 222, 232)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(24),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55)),
                    },
                    new TextBlock
                    {
                        Text = content,
                        Margin = new Thickness(0, 12, 0, 0),
                        FontSize = 14,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 96, 120)),
                    },
                },
            },
        });
    }

    private void SaveCurrentNote()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        SaveNoteIfChanged(EditorViewControl.GetContent());
    }

    private void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        string? sourcePath = e.SourcePath;

        try
        {
            string createdPath = noteService.CreateExtractFile(new CreateExtractFileRequest(
                currentNotesDirectory,
                currentNotePath ?? string.Empty,
                sourcePath ?? string.Empty,
                e.SelectedMarkdown));
            RefreshNotesTree(selectPath: currentNotePath);
            SendMentionTargets();
            EditorViewControl.PostExtractResult(sourcePath, ok: true, createdPath);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostExtractResult(sourcePath, ok: false, error: ex.Message);
        }
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        try
        {
            MarkdownMentionPreview preview = noteService.ReadMentionPreview(
                currentNotesDirectory,
                e.RelativePath,
                e.MaxCharacters);

            EditorViewControl.PostMentionPreviewResult(
                e.RequestId,
                preview.RelativePath,
                e.Mode,
                ok: true,
                title: preview.Title,
                markdownExcerpt: preview.MarkdownExcerpt);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: ex.Message);
        }
    }

    private async void EditorView_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        try
        {
            SaveMentionSource(e);

            string targetPath = noteService.ResolveMentionPath(currentNotesDirectory, e.RelativePath);
            await ShowEditorViewAsync(targetPath);
            SelectNote(targetPath);
            EditorViewControl.PostMentionOpenResult(e.RelativePath, ok: true);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostMentionOpenResult(e.RelativePath, ok: false, error: ex.Message);
        }
    }

    private async void RecentNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string notePath })
        {
            SaveCurrentNote();
            await ShowEditorViewAsync(notePath);
            SelectNote(notePath);
        }
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        if (!string.IsNullOrWhiteSpace(currentNotePath))
        {
            SaveNoteIfChanged(content);
        }
    }

    private void PopulateRecentNotes(IEnumerable<string> noteFiles)
    {
        RecentNotesPanel.Children.Clear();

        foreach (string filePath in noteFiles
                     .OrderByDescending(File.GetLastWriteTime)
                     .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                     .Take(5))
        {
            Button recentButton = new()
            {
                Content = Path.GetFileNameWithoutExtension(filePath),
                Tag = filePath,
                Style = (Style)FindResource("SidebarButtonStyle"),
            };

            recentButton.Click += RecentNoteButton_Click;
            RecentNotesPanel.Children.Add(recentButton);
        }
    }

    private void RefreshNotesTree(string? selectPath = null)
    {
        loadingNotesDirectory = true;

        try
        {
            NotesRoot.Items.Clear();

            List<string> noteFiles = Directory.EnumerateFiles(currentNotesDirectory, "*.md", SearchOption.AllDirectories)
                .ToList();

            PopulateRecentNotes(noteFiles);

            string? fullSelectPath = string.IsNullOrWhiteSpace(selectPath)
                ? null
                : Path.GetFullPath(selectPath);
            TreeViewItem? itemToSelect = null;
            foreach (string filePath in noteFiles.OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
            {
                TreeViewItem item = new()
                {
                    Header = Path.GetRelativePath(currentNotesDirectory, filePath),
                    Tag = filePath,
                };

                NotesRoot.Items.Add(item);

                if (fullSelectPath is not null
                    && string.Equals(Path.GetFullPath(filePath), fullSelectPath, StringComparison.OrdinalIgnoreCase))
                {
                    itemToSelect = item;
                }
            }

            NotesRoot.IsExpanded = true;

            if (itemToSelect is not null)
            {
                itemToSelect.IsSelected = true;
                itemToSelect.Focus();
            }
            else
            {
                NotesRoot.IsSelected = true;
            }
        }
        finally
        {
            loadingNotesDirectory = false;
        }
    }

    private void SelectNote(string notePath)
    {
        foreach (TreeViewItem item in NotesRoot.Items.OfType<TreeViewItem>())
        {
            if (string.Equals(item.Tag as string, notePath, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
                item.Focus();
                return;
            }
        }
    }

    private void SaveNoteIfChanged(string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return;
        }

        noteService.SaveNote(currentNotePath, content);
        lastSavedContent = content;
    }

    private void SaveMentionSource(OpenMentionRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(e.SourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The mention source is not the current Markdown file.");
        }

        SaveNoteIfChanged(e.Markdown);
    }

    private void SendMentionTargets()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory) || !Directory.Exists(currentNotesDirectory))
        {
            return;
        }

        IReadOnlyList<MarkdownMentionTarget> targets = noteService.ListMentionTargets(
            currentNotesDirectory,
            CreateRecentMentionRanks());

        EditorViewControl.PostMentionTargets(targets);
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

    private void ShowOverlay(UIElement overlay)
    {
        OverlayHost.Content = overlay;
        OverlayHost.Visibility = Visibility.Visible;
        OverlayHost.IsHitTestVisible = true;
    }

    private void HideOverlay()
    {
        OverlayHost.Content = null;
        OverlayHost.Visibility = Visibility.Collapsed;
        OverlayHost.IsHitTestVisible = false;
    }
}
