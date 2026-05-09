using System.Windows.Input;
using CogntApp.ViewModels.Decks.PresentationModels;
using CardLibraryRow = CogntApp.ViewModels.Decks.PresentationModels.CardLibraryRow;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel
{
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

    public object? ScrollableSubview => IsDirectSubview(CurrentSubview)
        ? null
        : CurrentSubview;

    public object? DirectSubview => IsDirectSubview(CurrentSubview)
        ? CurrentSubview
        : null;

    public bool IsDecksSelected => activeTab == FlashcardsTab.DecksCards;

    public bool IsCardsSelected => activeTab == FlashcardsTab.DecksCards;

    public bool IsStatisticsSelected => activeTab == FlashcardsTab.Statistics;

    private void SetActiveTab(FlashcardsTab tab)
    {
        RefreshFlashcards();

        object targetSubview = tab switch
        {
            FlashcardsTab.Statistics => Statistics,
            _ => Decks
        };

        NavigateToSubview(tab, targetSubview);
    }

    private void ShowCreateCardReview()
    {
        RefreshFlashcards();
        NavigateToSubview(FlashcardsTab.DecksCards, CreateCardReview);
    }

    private void ShowStatistics()
    {
        SetActiveTab(FlashcardsTab.Statistics);
    }

    private void ShowEditCard(CardLibraryRow card)
    {
        EditCard.SetCard(card);
        NavigateToSubview(FlashcardsTab.DecksCards, EditCard);
    }

    private void NotifySelectedTabChanged()
    {
        OnPropertyChanged(nameof(IsDecksSelected));
        OnPropertyChanged(nameof(IsCardsSelected));
        OnPropertyChanged(nameof(IsStatisticsSelected));
    }

    private bool IsDirectSubview(object subview)
    {
        return ReferenceEquals(subview, CreateCardReview)
            || ReferenceEquals(subview, EditCard);
    }

    private void Cards_FiltersChanged(object? sender, EventArgs e)
    {
        RefreshFlashcards();
    }

    private void Decks_SelectedDecksChanged(object? sender, EventArgs e)
    {
        RefreshFlashcards();
    }

    private void NavigateToSubview(FlashcardsTab tab, object targetSubview)
    {
        ArgumentNullException.ThrowIfNull(targetSubview);

        if (activeTab == tab && ReferenceEquals(CurrentSubview, targetSubview))
        {
            return;
        }

        activeTab = tab;
        CurrentSubview = targetSubview;
        NotifySelectedTabChanged();
    }

    private enum FlashcardsTab
    {
        DecksCards,
        Statistics
    }
}
