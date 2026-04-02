using System.Windows.Controls;
using CogntApp.Models.Decks;

namespace CogntApp.Views;

public partial class DecksView : UserControl
{
    public event EventHandler? PracticeRequested;

    public event EventHandler<CreateCardEditorHostRequestedEventArgs>? CreateCardEditorHostRequested;

    public event EventHandler? CreateCardEditorReleased;

    public DecksView()
    {
        InitializeComponent();
    }

    private void PracticeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        PracticeRequested?.Invoke(this, EventArgs.Empty);
    }
}
