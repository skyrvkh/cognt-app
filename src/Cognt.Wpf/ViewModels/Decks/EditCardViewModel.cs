using System.Windows.Input;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed class EditCardViewModel : ObservableObject
{
    public EditCardViewModel(Action backToCards)
    {
        ArgumentNullException.ThrowIfNull(backToCards);

        BackToCardsCommand = new RelayCommand(backToCards);
    }

    public ICommand BackToCardsCommand { get; }

    public CardLibraryRow? Card { get; private set; }

    public string FrontContent => Card?.FrontContent ?? string.Empty;

    public bool CanSaveChanges => Card is { CanEdit: true };

    internal SaveCardRequestedEventArgs BuildSaveRequest(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        CardLibraryRow card = Card
            ?? throw new InvalidOperationException("A card must be selected before saving.");
        if (!card.CanEdit)
        {
            throw new InvalidOperationException("The selected card cannot be edited.");
        }

        return new SaveCardRequestedEventArgs(card.Id, content);
    }

    public void SetCard(CardLibraryRow card)
    {
        ArgumentNullException.ThrowIfNull(card);

        Card = card;
        OnPropertyChanged(nameof(Card));
        OnPropertyChanged(nameof(FrontContent));
        OnPropertyChanged(nameof(CanSaveChanges));
    }
}
