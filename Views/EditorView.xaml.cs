using System.Windows.Controls;

namespace CogntApp.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
    }

    public void ShowNote(string title, string content)
    {
        NoteTitle.Text = title;
        NoteEditor.Text = content;
    }

    public string GetContent()
    {
        return NoteEditor.Text;
    }
}
