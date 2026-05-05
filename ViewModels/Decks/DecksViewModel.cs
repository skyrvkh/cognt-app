using System.Windows.Input;

namespace CogntApp.ViewModels.Decks;

public sealed class DecksViewModel : ObservableObject
{
    private object currentSubview;
    private FlashcardsTab activeTab = FlashcardsTab.Decks;

    public DecksViewModel()
    {
        Decks = new DecksSubviewViewModel();
        Cards = new CardsSubviewViewModel(ShowCreateCardReview);
        CreateCardReview = new CreateCardReviewSubviewViewModel(() => SetActiveTab(FlashcardsTab.Cards), string.Empty);
        Statistics = new StatisticsSubviewViewModel();

        currentSubview = Decks;
        ShowDecksCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Decks));
        ShowCardsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Cards));
        ShowStatisticsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.Statistics));
    }

    public DecksSubviewViewModel Decks { get; }

    public CardsSubviewViewModel Cards { get; }

    public CreateCardReviewSubviewViewModel CreateCardReview { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public ICommand ShowDecksCommand { get; }

    public ICommand ShowCardsCommand { get; }

    public ICommand ShowStatisticsCommand { get; }

    public object CurrentSubview
    {
        get => currentSubview;
        private set
        {
            if (!SetProperty(ref currentSubview, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ScrollableSubview));
            OnPropertyChanged(nameof(DirectSubview));
        }
    }

    public object? ScrollableSubview => ReferenceEquals(CurrentSubview, CreateCardReview)
        ? null
        : CurrentSubview;

    public object? DirectSubview => ReferenceEquals(CurrentSubview, CreateCardReview)
        ? CurrentSubview
        : null;

    public bool IsDecksSelected => activeTab == FlashcardsTab.Decks;

    public bool IsCardsSelected => activeTab == FlashcardsTab.Cards;

    public bool IsStatisticsSelected => activeTab == FlashcardsTab.Statistics;

    private void SetActiveTab(FlashcardsTab tab)
    {
        object targetSubview = tab switch
        {
            FlashcardsTab.Cards => Cards,
            FlashcardsTab.Statistics => Statistics,
            _ => Decks
        };

        if (activeTab == tab && ReferenceEquals(CurrentSubview, targetSubview))
        {
            return;
        }

        activeTab = tab;
        CurrentSubview = targetSubview;

        OnPropertyChanged(nameof(IsDecksSelected));
        OnPropertyChanged(nameof(IsCardsSelected));
        OnPropertyChanged(nameof(IsStatisticsSelected));
    }

    private void ShowCreateCardReview()
    {
        activeTab = FlashcardsTab.Cards;
        CurrentSubview = CreateCardReview;

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
