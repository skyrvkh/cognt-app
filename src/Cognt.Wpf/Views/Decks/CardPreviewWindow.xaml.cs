using System.Windows;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Views.Decks;

public partial class CardPreviewWindow : Window
{
    public CardPreviewWindow(CardLibraryRow card)
    {
        ArgumentNullException.ThrowIfNull(card);

        InitializeComponent();
        DataContext = new CardPreviewViewModel(card);
    }
}
