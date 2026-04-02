using System.Windows.Input;

namespace CogntApp.ViewModels.Decks;

public sealed class CreateCardReviewSubviewViewModel
{
    public CreateCardReviewSubviewViewModel(Action backToCards)
    {
        BackToCardsCommand = new RelayCommand(backToCards);
    }

    public ICommand BackToCardsCommand { get; }
}
