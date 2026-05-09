using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel : ObservableObject
{
    private readonly IDecksWorkspaceDataProvider workspaceDataProvider;
    private readonly ICreateFlashcardsCommand createFlashcardsCommand;
    private readonly IUpdateFlashcardFrontContentCommand updateFlashcardFrontContentCommand;
    private object currentSubview;
    private FlashcardsTab activeTab = FlashcardsTab.DecksCards;
    private string collectionId = string.Empty;
    private string? sourceRelPath;
    private string practiceDueCount = "0";

    internal event EventHandler<UserMessageRequestedEventArgs>? InfoMessageRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    internal DecksViewModel(
        IDecksWorkspaceDataProvider workspaceDataProvider,
        ICreateFlashcardsCommand createFlashcardsCommand,
        IUpdateFlashcardFrontContentCommand updateFlashcardFrontContentCommand,
        IKnowledgeDocumentContentFactory documentContentFactory,
        StatisticsSubviewViewModel statistics)
    {
        ArgumentNullException.ThrowIfNull(workspaceDataProvider);
        ArgumentNullException.ThrowIfNull(createFlashcardsCommand);
        ArgumentNullException.ThrowIfNull(updateFlashcardFrontContentCommand);
        ArgumentNullException.ThrowIfNull(documentContentFactory);
        ArgumentNullException.ThrowIfNull(statistics);

        this.workspaceDataProvider = workspaceDataProvider;
        this.createFlashcardsCommand = createFlashcardsCommand;
        this.updateFlashcardFrontContentCommand = updateFlashcardFrontContentCommand;

        Cards = new CardsSubviewViewModel(ShowCreateCardReview, ShowEditCard);
        Cards.FiltersChanged += Cards_FiltersChanged;
        CreateCardReview = new CreateCardReviewSubviewViewModel(
            () => SetActiveTab(FlashcardsTab.DecksCards),
            documentContentFactory.FromPlainText(string.Empty, "Card Text"));
        EditCard = new EditCardViewModel(() => SetActiveTab(FlashcardsTab.DecksCards));
        Statistics = statistics;
        Statistics.SetBackToDecksAction(() => SetActiveTab(FlashcardsTab.DecksCards));
        Decks = new DecksSubviewViewModel(ShowCreateCardReview, ShowStatistics, Cards, Statistics);
        Decks.SelectedDecksChanged += Decks_SelectedDecksChanged;

        currentSubview = Decks;
        ShowDecksCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.DecksCards));
        ShowCardsCommand = new RelayCommand(() => SetActiveTab(FlashcardsTab.DecksCards));
        ShowStatisticsCommand = new RelayCommand(ShowStatistics);
    }

    public DecksSubviewViewModel Decks { get; }

    public CardsSubviewViewModel Cards { get; }

    public CreateCardReviewSubviewViewModel CreateCardReview { get; }

    public EditCardViewModel EditCard { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public string PracticeDueCount
    {
        get => practiceDueCount;
        private set => SetProperty(ref practiceDueCount, value);
    }
}
