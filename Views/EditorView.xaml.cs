using System.Windows.Controls;
using System.Windows.Documents;

namespace CogntApp.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
    }

    public void ShowNote(string title, string content)
    {
        NoteEditor.Document = new FlowDocument(
            new Paragraph(
                new Run(title)
                {
                    FontSize = 22,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                }));

        NoteEditor.Document.Blocks.Add(new Paragraph(new Run(content)));
    }
}
