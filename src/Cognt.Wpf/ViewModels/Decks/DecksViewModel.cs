using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel : ObservableObject
{
    private readonly IDecksWorkspaceDataProvider workspaceDataProvider;
    private readonly IUpdateFlashcardSchedulingCommand updateFlashcardSchedulingCommand;
    private readonly IDeleteFlashcardsCommand deleteFlashcardsCommand;
    private readonly IUpdateFlashcardTagsCommand updateFlashcardTagsCommand;
    private object currentSubview;
    private FlashcardsTab activeTab = FlashcardsTab.DecksCards;
    private string collectionId = string.Empty;
    private string practiceDueCount = "0";
    private string cramCardCount = "0";

    internal event EventHandler<UserMessageRequestedEventArgs>? InfoMessageRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    internal DecksViewModel(
        IDecksWorkspaceDataProvider workspaceDataProvider,
        IUpdateFlashcardSchedulingCommand updateFlashcardSchedulingCommand,
        IDeleteFlashcardsCommand deleteFlashcardsCommand,
        IUpdateFlashcardTagsCommand updateFlashcardTagsCommand,
        StatisticsSubviewViewModel statistics)
    {
        ArgumentNullException.ThrowIfNull(workspaceDataProvider);
        ArgumentNullException.ThrowIfNull(updateFlashcardSchedulingCommand);
        ArgumentNullException.ThrowIfNull(deleteFlashcardsCommand);
        ArgumentNullException.ThrowIfNull(updateFlashcardTagsCommand);
        ArgumentNullException.ThrowIfNull(statistics);

        this.workspaceDataProvider = workspaceDataProvider;
        this.updateFlashcardSchedulingCommand = updateFlashcardSchedulingCommand;
        this.deleteFlashcardsCommand = deleteFlashcardsCommand;
        this.updateFlashcardTagsCommand = updateFlashcardTagsCommand;

        Cards = new CardsSubviewViewModel(
            SetCardSuspensionAsync,
            PostponeCardsAsync,
            DeleteCardsAsync,
            AddTagsToCardsAsync,
            RemoveTagsFromCardsAsync);
        Cards.FiltersChanged += Cards_FiltersChanged;
        Statistics = statistics;
        Statistics.SetBackToDecksAction(() => SetActiveTab(FlashcardsTab.DecksCards));
        Decks = new DecksSubviewViewModel(ShowStatistics, Cards, Statistics);
        Decks.SelectedDecksChanged += Decks_SelectedDecksChanged;

        currentSubview = Decks;
        ShowDecksCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.DecksCards));
        ShowCardsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.DecksCards));
        ShowStatisticsCommand = new RelayCommand(ShowStatistics);
    }

    public DecksSubviewViewModel Decks { get; }

    public CardsSubviewViewModel Cards { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public string PracticeDueCount
    {
        get => practiceDueCount;
        private set => SetProperty(ref practiceDueCount, value);
    }

    public string CramCardCount
    {
        get => cramCardCount;
        private set => SetProperty(ref cramCardCount, value);
    }

    internal CramScope CreateCramScope()
    {
        return new CramScope
        {
            DeckIds = Decks.SelectedDeckIds.ToHashSet(),
            IncludeSubdecks = true,
            ExcludeSuspendedCards = true,
        };
    }
}
