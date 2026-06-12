using System.Windows.Controls;

namespace CogntApp.Views;

public partial class NoteInspectorView : UserControl
{
    public NoteInspectorView()
    {
        InitializeComponent();
        OutlineListBox.SelectionChanged += OutlineListBox_SelectionChanged;
    }

    private void OutlineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutlineListBox.SelectedItem is not null)
        {
            OutlineListBox.ScrollIntoView(OutlineListBox.SelectedItem);
        }
    }
}
