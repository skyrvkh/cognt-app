using System.Windows.Input;

namespace CogntApp.ViewModels.Decks;

public sealed class DecksViewModel : ObservableObject
{
    private object currentSubview;
    private FlashcardsTab activeTab = FlashcardsTab.Decks;

    public DecksViewModel()
    {
        Decks = new DecksSubviewViewModel();
        Cards = new CardsSubviewViewModel();
        Statistics = new StatisticsSubviewViewModel();

        currentSubview = Decks;
        ShowDecksCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Decks));
        ShowCardsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Cards));
        ShowStatisticsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Statistics));
    }

    public DecksSubviewViewModel Decks { get; }

    public CardsSubviewViewModel Cards { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public ICommand ShowDecksCommand { get; }

    public ICommand ShowCardsCommand { get; }

    public ICommand ShowStatisticsCommand { get; }

    public object CurrentSubview
    {
        get => currentSubview;
        private set => SetProperty(ref currentSubview, value);
    }

    public bool IsDecksSelected => activeTab == FlashcardsTab.Decks;

    public bool IsCardsSelected => activeTab == FlashcardsTab.Cards;

    public bool IsStatisticsSelected => activeTab == FlashcardsTab.Statistics;

    private void SetActiveTab(FlashcardsTab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        CurrentSubview = tab switch
        {
            FlashcardsTab.Cards => Cards,
            FlashcardsTab.Statistics => Statistics,
            _ => Decks
        };

        OnPropertyChanged(nameof(IsDecksSelected));
        OnPropertyChanged(nameof(IsCardsSelected));
        OnPropertyChanged(nameof(IsStatisticsSelected));
    }

    private enum FlashcardsTab
    {
        Decks,
        Cards,
        Statistics
    }
}
