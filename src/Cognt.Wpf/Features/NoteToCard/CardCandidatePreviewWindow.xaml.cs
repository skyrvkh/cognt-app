using System.Windows;

namespace CogntApp.Features.NoteToCard;

public partial class CardCandidatePreviewWindow : Window
{
    public CardCandidatePreviewWindow(CardCandidatePreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        InitializeComponent();
        DataContext = preview;
    }
}
