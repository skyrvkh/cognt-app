using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Wpf.EditorHost;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel : ObservableObject
{
    private readonly IDecksWorkspaceDataProvider workspaceDataProvider;
    private readonly IDecksCardActionService cardActions;
    private readonly INoteService noteService;
    private readonly ILogger<DecksViewModel> logger;
    private object currentSubview;
    private FlashcardsTab activeTab = FlashcardsTab.DecksCards;
    private string collectionId = string.Empty;
    private string practiceDueCount = "0";
    private string cramCardCount = "0";
    private bool isCramEnabled;

    internal event EventHandler<UserMessageRequestedEventArgs>? InfoMessageRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    internal DecksViewModel(
        IDecksWorkspaceDataProvider workspaceDataProvider,
        IDecksCardActionService cardActions,
        INoteService noteService,
        IEditorViewFactory editorViewFactory,
        StatisticsSubviewViewModel statistics,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(workspaceDataProvider);
        ArgumentNullException.ThrowIfNull(cardActions);
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(editorViewFactory);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.workspaceDataProvider = workspaceDataProvider;
        this.cardActions = cardActions;
        this.noteService = noteService;
        logger = loggerFactory.CreateLogger<DecksViewModel>();

        Cards = new CardsSubviewViewModel(
            SetCardsSuspensionAsync,
            PostponeCardsAsync,
            UpdateCardContentAsync,
            DeleteCardsAsync,
            AddTagsToCardsAsync,
            RemoveTagsFromCardsAsync,
            LoadAvailableTagNames,
            editorViewFactory.Create);
        Cards.FiltersChanged += Cards_FiltersChanged;
        Statistics = statistics;
        Statistics.SetBackToDecksAction(() => SetActiveTab(FlashcardsTab.DecksCards));
        Decks = new DecksSubviewViewModel(
            ShowStatistics,
            DeleteDecksAsync,
            Cards,
            Statistics);
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

    public bool IsCramEnabled
    {
        get => isCramEnabled;
        private set => SetProperty(ref isCramEnabled, value);
    }

    internal void SetCramEnabled(bool isEnabled)
    {
        IsCramEnabled = isEnabled;
    }

    internal CramScope CreateCramScope()
    {
        CramScope scope = new()
        {
            DeckIds = Decks.SelectedDeckIds,
            IncludeSubdecks = true,
            ExcludeSuspendedCards = true,
        };
        return scope;
    }
}
