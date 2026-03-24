using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CogntApp.Views;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, string> noteContent = new()
    {
        ["Notes"] = "Select a note from the tree to begin writing.",
        ["Inbox"] = "Capture loose thoughts here before organizing them.",
        ["Learning"] = "Keep study notes, concepts, and active recall prompts here.",
        ["Projects"] = "Collect project notes, decisions, and next actions here.",
    };

    private readonly EditorView editorView = new();
    private readonly DecksView decksView = new();
    private readonly ReviewView reviewView = new();
    private string currentNoteName = "Notes";

    public MainWindow()
    {
        InitializeComponent();
        ShowEditorView();
    }

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem selectedItem && selectedItem.Header is string noteName)
        {
            ShowEditorView(noteName);
        }
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
        const string noteName = "Untitled";

        noteContent[noteName] = noteName;

        TreeViewItem noteItem = new()
        {
            Header = noteName,
        };

        NotesRoot.Items.Add(noteItem);
        NotesRoot.IsExpanded = true;
        noteItem.IsSelected = true;
        ShowEditorView(noteName);
    }

    private void SaveNoteButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
    }

    private void ShowEditorView(string noteName = "Notes")
    {
        currentNoteName = noteName;
        string content = noteContent.GetValueOrDefault(noteName, string.Empty);
        editorView.ShowNote(noteName, content);
        ViewHost.Content = editorView;
    }

    private void SaveCurrentNote()
    {
        noteContent[currentNoteName] = editorView.GetContent();
    }
}
