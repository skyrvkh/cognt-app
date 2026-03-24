using System.IO;
using System.Windows;
using System.Windows.Controls;
using CogntApp.Views;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly EditorView editorView = new();
    private readonly DecksView decksView = new();
    private readonly ReviewView reviewView = new();
    private string currentNotesDirectory = string.Empty;
    private string? currentNotePath;

    public MainWindow()
    {
        InitializeComponent();
        LoadNotesDirectory(ResolveDefaultNotesDirectory());
    }

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem selectedItem)
        {
            return;
        }

        if (selectedItem.Tag is string notePath)
        {
            ShowEditorView(notePath);
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
        ViewHost.Content = reviewView;
    }

    private void DecksButton_Click(object sender, RoutedEventArgs e)
    {
        ViewHost.Content = decksView;
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            return;
        }

        string notePath = CreateUniqueNotePath();
        File.WriteAllText(notePath, "# Untitled" + Environment.NewLine);
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
        NotesDirectoryTextBox.Text = currentNotesDirectory;
        NotesRoot.Header = GetDirectoryName(currentNotesDirectory);
        NotesRoot.Items.Clear();

        List<string> noteFiles = Directory.EnumerateFiles(currentNotesDirectory, "*.md", SearchOption.AllDirectories)
            .ToList();

        PopulateRecentNotes(noteFiles);

        foreach (string filePath in noteFiles.OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
        {
            NotesRoot.Items.Add(new TreeViewItem
            {
                Header = Path.GetRelativePath(currentNotesDirectory, filePath),
                Tag = filePath,
            });
        }

        NotesRoot.IsExpanded = true;

        if (NotesRoot.Items.OfType<TreeViewItem>().FirstOrDefault() is { } firstNote)
        {
            firstNote.IsSelected = true;
        }
        else
        {
            ShowDirectoryOverview();
        }
    }

    private void ShowEditorView(string notePath)
    {
        currentNotePath = notePath;
        editorView.ShowNote(Path.GetFileName(notePath), File.ReadAllText(notePath));
        ViewHost.Content = editorView;
    }

    private void ShowDirectoryOverview()
    {
        currentNotePath = null;
        int noteCount = NotesRoot.Items.Count;
        string title = GetDirectoryName(currentNotesDirectory);
        string content = noteCount == 1
            ? "1 Markdown file found."
            : $"{noteCount} Markdown files found.";

        editorView.ShowNote(title, content);
        ViewHost.Content = editorView;
    }

    private void SaveCurrentNote()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        File.WriteAllText(currentNotePath, editorView.GetContent());
    }

    private void RecentNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string notePath })
        {
            ShowEditorView(notePath);
            SelectNote(notePath);
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

    private string CreateUniqueNotePath()
    {
        string notePath = Path.Combine(currentNotesDirectory, "untitled.md");
        int suffix = 2;

        while (File.Exists(notePath))
        {
            notePath = Path.Combine(currentNotesDirectory, $"untitled-{suffix}.md");
            suffix++;
        }

        return notePath;
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
