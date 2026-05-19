using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Design;
using CogntApp.Features.NoteToCard;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Collections;
using Navigation = CogntApp.ViewModels.Navigation;
using PkmNoteService = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.INoteService;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly PkmNoteService noteService;
    private readonly IKnowledgeDocumentContentFactory documentContentFactory;
    private readonly IKnowledgeCollectionPathResolver collectionPathResolver;
    private readonly IGlobalSearchService globalSearchService;
    private readonly ICollectionProgressAnalyticsDashboardQuery collectionProgressAnalyticsDashboardQuery;
    private readonly INoteToCardQueueQuery noteToCardQueueQuery;
    private readonly IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery;
    private readonly ICreateCardsFromCardContextCommand createCardsFromCardContextCommand;
    private readonly IFlashcardReviewSettingsQuery flashcardReviewSettingsQuery;
    private readonly IUpdateFlashcardReviewSettingsCommand updateFlashcardReviewSettingsCommand;
    private readonly IThemeService themeService;
    private readonly IEditorSettingsService editorSettingsService;
    private readonly SemaphoreSlim noteSaveSemaphore = new(1, 1);
    private string collectionPath = string.Empty;
    private string openCollectionPath = string.Empty;
    private string? currentNotePath;
    private long? currentNoteItemId;
    private KnowledgePdfDocumentReadModel? currentPdfDocument;
    private string currentNoteTitle = string.Empty;
    private string lastSavedContent = string.Empty;
    private bool isLoadingCollection;
    private int currentCollectionNoteCount;
    private int currentCollectionCardCount;
    private bool isEditorReadOnly;
    private double globalTextFontSize = 11;
    private ThemeOptionViewModel selectedThemeOption = null!;
    public MainViewModel(
        PkmNoteService noteService,
        IKnowledgeDocumentContentFactory documentContentFactory,
        IKnowledgeCollectionPathResolver collectionPathResolver,
        IGlobalSearchService globalSearchService,
        ICollectionProgressAnalyticsDashboardQuery collectionProgressAnalyticsDashboardQuery,
        INoteToCardQueueQuery noteToCardQueueQuery,
        IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery,
        ICreateCardsFromCardContextCommand createCardsFromCardContextCommand,
        IFlashcardReviewSettingsQuery flashcardReviewSettingsQuery,
        IUpdateFlashcardReviewSettingsCommand updateFlashcardReviewSettingsCommand,
        IThemeService themeService,
        IEditorSettingsService editorSettingsService)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(documentContentFactory);
        ArgumentNullException.ThrowIfNull(collectionPathResolver);
        ArgumentNullException.ThrowIfNull(globalSearchService);
        ArgumentNullException.ThrowIfNull(collectionProgressAnalyticsDashboardQuery);
        ArgumentNullException.ThrowIfNull(noteToCardQueueQuery);
        ArgumentNullException.ThrowIfNull(flashcardDeckSummariesQuery);
        ArgumentNullException.ThrowIfNull(createCardsFromCardContextCommand);
        ArgumentNullException.ThrowIfNull(flashcardReviewSettingsQuery);
        ArgumentNullException.ThrowIfNull(updateFlashcardReviewSettingsCommand);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(editorSettingsService);

        this.noteService = noteService;
        this.documentContentFactory = documentContentFactory;
        this.collectionPathResolver = collectionPathResolver;
        this.globalSearchService = globalSearchService;
        this.collectionProgressAnalyticsDashboardQuery = collectionProgressAnalyticsDashboardQuery;
        this.noteToCardQueueQuery = noteToCardQueueQuery;
        this.flashcardDeckSummariesQuery = flashcardDeckSummariesQuery;
        this.createCardsFromCardContextCommand = createCardsFromCardContextCommand;
        this.flashcardReviewSettingsQuery = flashcardReviewSettingsQuery;
        this.updateFlashcardReviewSettingsCommand = updateFlashcardReviewSettingsCommand;
        this.themeService = themeService;
        this.editorSettingsService = editorSettingsService;

        InitializeThemeOptions();
        InitializeEditorSettings();
        InitializeCommands();
        InitializeNoteToCardPresentationOptions();
    }

    internal event EventHandler<Navigation.OverlayRequestedEventArgs>? OverlayRequested;

    internal event EventHandler? ClearEditorRequested;

    internal event EventHandler<Navigation.OpenNoteRequestedEventArgs>? OpenNoteRequested;

    internal event EventHandler<Navigation.NoteDocumentsDeletedEventArgs>? NoteDocumentsDeleted;

    internal event EventHandler<Navigation.OpenPdfRequestedEventArgs>? OpenPdfRequested;

    internal event EventHandler? MentionTargetsChanged;

    internal event EventHandler<Navigation.UserMessageRequestedEventArgs>? UserMessageRequested;

    internal event EventHandler<CardCandidatePreviewRequestedEventArgs>? CardCandidatePreviewRequested;

    internal event EventHandler? CreateCardContextFromSelectionRequested;

    internal event EventHandler? AddSelectedTextToSelectedCardSlotRequested;

    internal event EventHandler<string>? CurrentNoteTitleChanged;

    internal event EventHandler<string>? CurrentNoteContentChanged;

    internal event EventHandler<bool>? EditorReadOnlyChanged;

    internal event EventHandler? AutoSaveSettingsChanged;

    public Func<string>? GetCurrentEditorContent { get; set; }

    public ObservableCollection<NoteTreeNodeViewModel> NoteRoots { get; } = [];

    public ObservableCollection<RecentNoteViewModel> RecentNotes { get; } = [];

    public ObservableCollection<DashboardMetricViewModel> DashboardMetrics { get; } = [];

    public ObservableCollection<DashboardStudyHeatmapCellViewModel> DashboardStudyHeatmapCells { get; } = [];

    public ObservableCollection<DashboardEmptyStateViewModel> DashboardEmptyStates { get; } = [];

    public ObservableCollection<Navigation.EditorBreadcrumbItemViewModel> EditorBreadcrumbItems { get; } = [];

    public ObservableCollection<Navigation.CollectionOptionViewModel> AvailableCollections { get; } = [];

    public ObservableCollection<GlobalSearchResultReadModel> GlobalSearchResults { get; } = [];

    public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; } = [];

    public string CollectionPath
    {
        get => collectionPath;
        set
        {
            if (SetProperty(ref collectionPath, value))
            {
                OnPropertyChanged(nameof(CurrentCollectionPath));
                OnPropertyChanged(nameof(CurrentCollectionName));
            }
        }
    }

    public string OpenCollectionPath => openCollectionPath;

    public string CurrentCollectionPath => string.IsNullOrWhiteSpace(openCollectionPath)
        ? CollectionPath
        : openCollectionPath;

    public string CurrentCollectionName => FormatCollectionName(CurrentCollectionPath);

    public string CurrentCollectionSummary => string.IsNullOrWhiteSpace(openCollectionPath)
        ? "No collection open"
        : $"{CountDisplayFormatter.Format(currentCollectionNoteCount, "note")} • {CountDisplayFormatter.Format(currentCollectionCardCount, "card")}";

    public bool HasOpenCollection => !string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath);

    public bool ShouldShowSettingsEmptyState => !HasOpenCollection;

    public string? CurrentNotePath => currentNotePath;

    public string? CurrentNoteRelativePath => TryGetCurrentNoteRelativePath();

    public bool HasAvailableCollections => AvailableCollections.Count > 0;

    public bool ShouldShowNoCollectionsState => !HasAvailableCollections;

    public bool ShouldShowNoNotesState =>
        !IsLoadingCollection
        && !string.IsNullOrWhiteSpace(openCollectionPath)
        && NoteRoots.Count == 0;

    public bool IsLoadingCollection
    {
        get => isLoadingCollection;
        private set
        {
            if (SetProperty(ref isLoadingCollection, value))
            {
                OnPropertyChanged(nameof(ShouldShowNoNotesState));
            }
        }
    }

    public bool IsEditorReadOnly
    {
        get => isEditorReadOnly;
        set
        {
            if (SetProperty(ref isEditorReadOnly, value))
            {
                EditorReadOnlyChanged?.Invoke(this, isEditorReadOnly);
            }
        }
    }

    public double GlobalTextFontSize
    {
        get => globalTextFontSize;
        set
        {
            double normalized = Math.Clamp(Math.Round(value, 0), 9, 18);
            SetProperty(ref globalTextFontSize, normalized);
        }
    }

    public ThemeOptionViewModel SelectedThemeOption
    {
        get => selectedThemeOption;
        set
        {
            if (value is null || ReferenceEquals(selectedThemeOption, value))
            {
                return;
            }

            selectedThemeOption = value;
            themeService.ApplyTheme(value.Theme);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentThemeDescription));
            OnPropertyChanged(nameof(CurrentThemeName));
            OnPropertyChanged(nameof(IsGraphiteTheme));
        }
    }

    public string CurrentThemeDescription => SelectedThemeOption.Description;

    public string CurrentThemeName => SelectedThemeOption.Name;

    public bool IsGraphiteTheme
    {
        get => SelectedThemeOption.Theme == ApplicationTheme.Graphite;
        set
        {
            ApplicationTheme theme = value ? ApplicationTheme.Graphite : ApplicationTheme.Light;
            ThemeOptionViewModel option = ThemeOptions.First(option => option.Theme == theme);
            SelectedThemeOption = option;
            OnPropertyChanged();
        }
    }
}
