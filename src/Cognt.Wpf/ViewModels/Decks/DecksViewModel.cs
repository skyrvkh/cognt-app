using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Wpf.EditorHost;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel : ObservableObject
{
    private readonly IDecksWorkspaceDataProvider workspaceDataProvider;
    private readonly IUpdateFlashcardSchedulingCommand updateFlashcardSchedulingCommand;
    private readonly IUpdateFlashcardContentCommand updateFlashcardContentCommand;
    private readonly IDeleteFlashcardsCommand deleteFlashcardsCommand;
    private readonly IUpdateFlashcardTagsCommand updateFlashcardTagsCommand;
    private readonly INoteService noteService;
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
        IUpdateFlashcardContentCommand updateFlashcardContentCommand,
        IDeleteFlashcardsCommand deleteFlashcardsCommand,
        IUpdateFlashcardTagsCommand updateFlashcardTagsCommand,
        INoteService noteService,
        IEditorViewFactory editorViewFactory,
        StatisticsSubviewViewModel statistics)
    {
        ArgumentNullException.ThrowIfNull(workspaceDataProvider);
        ArgumentNullException.ThrowIfNull(updateFlashcardSchedulingCommand);
        ArgumentNullException.ThrowIfNull(updateFlashcardContentCommand);
        ArgumentNullException.ThrowIfNull(deleteFlashcardsCommand);
        ArgumentNullException.ThrowIfNull(updateFlashcardTagsCommand);
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(editorViewFactory);
        ArgumentNullException.ThrowIfNull(statistics);

        this.workspaceDataProvider = workspaceDataProvider;
        this.updateFlashcardSchedulingCommand = updateFlashcardSchedulingCommand;
        this.updateFlashcardContentCommand = updateFlashcardContentCommand;
        this.deleteFlashcardsCommand = deleteFlashcardsCommand;
        this.updateFlashcardTagsCommand = updateFlashcardTagsCommand;
        this.noteService = noteService;

        Cards = new CardsSubviewViewModel(
            SetCardSuspensionAsync,
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
