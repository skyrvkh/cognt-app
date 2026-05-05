using System.Windows.Controls;
using CogntApp.Models.Decks;
using CogntApp.Views.Decks;

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

    private void CreateCardReviewSubview_CardEditorHostRequested(
        object sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        if (sender is CreateCardReviewSubview)
        {
            CreateCardEditorHostRequested?.Invoke(this, e);
        }
    }

    private void CreateCardReviewSubview_CardEditorReleased(object sender, EventArgs e)
    {
        if (sender is CreateCardReviewSubview)
        {
            CreateCardEditorReleased?.Invoke(this, EventArgs.Empty);
        }
    }
}
