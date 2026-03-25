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
    private readonly DashboardView dashboardView = new();
    private readonly DecksView decksView = new();
    private readonly ReviewView reviewView = new();
    private string currentNotesDirectory = string.Empty;
    private string? currentNotePath;
    private bool loadingNotesDirectory;
    private int editorOpenRequestId;

    public MainWindow()
    {
        InitializeComponent();
        EditorViewControl.ContentChanged += EditorView_ContentChanged;
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
        ShowOverlay(reviewView);
    }

    private void DecksButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
        editorOpenRequestId++;
        currentNotePath = null;
        ShowOverlay(decksView);
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
        loadingNotesDirectory = true;

        try
        {
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
            NotesRoot.IsSelected = true;
        }
        finally
        {
            loadingNotesDirectory = false;
        }

        ShowDirectoryOverview();
    }

    private async Task ShowEditorViewAsync(string notePath)
    {
        int requestId = ++editorOpenRequestId;
        string content = File.ReadAllText(notePath);

        try
        {
            await EditorViewControl.LoadNoteAsync(Path.GetFileName(notePath), content);
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
        HideOverlay();
    }

    private void ShowDashboardView()
    {
        editorOpenRequestId++;
        currentNotePath = null;
        ShowOverlay(dashboardView);
    }

    private void ShowDirectoryOverview()
    {
        editorOpenRequestId++;
        currentNotePath = null;
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

        File.WriteAllText(currentNotePath, EditorViewControl.GetContent());
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
            File.WriteAllText(currentNotePath, content);
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
