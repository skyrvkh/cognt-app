using System.Windows;
using CogntApp.Models.Decks;
using CogntApp.ViewModels.Decks;

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
