using System.Windows.Controls;

namespace CogntApp.Features.NoteToCard;

public partial class NoteCardQueueView : UserControl
{
    public NoteCardQueueView()
    {
        InitializeComponent();
    }

    public event EventHandler? CreateClozeRequested;

    public event EventHandler? OpenImageOcclusionRequested;

    private void CreateClozeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CreateClozeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenImageOcclusionButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenImageOcclusionRequested?.Invoke(this, EventArgs.Empty);
    }
}
