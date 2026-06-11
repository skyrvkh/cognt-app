using System.Collections.ObjectModel;
using System.Globalization;
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
using Microsoft.Extensions.Logging;
using Navigation = CogntApp.ViewModels.Navigation;
using PkmNoteService = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.INoteService;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly PkmNoteService noteService;
    private readonly IKnowledgeDocumentContentFactory documentContentFactory;
    private readonly IKnowledgeCollectionPathResolver collectionPathResolver;
    private readonly IKnownCollectionStore knownCollectionStore;
    private readonly IGlobalSearchService globalSearchService;
    private readonly ICollectionProgressAnalyticsDashboardQuery collectionProgressAnalyticsDashboardQuery;
    private readonly INoteToCardQueueQuery noteToCardQueueQuery;
    private readonly IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery;
    private readonly IFlashcardCardsQuery flashcardCardsQuery;
    private readonly ICreateCardsFromCardContextCommand createCardsFromCardContextCommand;
    private readonly IImportFlashcardCollectionCommand importFlashcardCollectionCommand;
    private readonly IFlashcardReviewSettingsQuery flashcardReviewSettingsQuery;
    private readonly IUpdateFlashcardReviewSettingsCommand updateFlashcardReviewSettingsCommand;
    private readonly IThemeService themeService;
    private readonly IEditorSettingsService editorSettingsService;
    private readonly ILogger<MainViewModel>? logger;
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
    private string globalTextFontSizeText = "11";
    private ThemeOptionViewModel selectedThemeOption = null!;
    public MainViewModel(
        PkmNoteService noteService,
        IKnowledgeDocumentContentFactory documentContentFactory,
        IKnowledgeCollectionPathResolver collectionPathResolver,
        IKnownCollectionStore knownCollectionStore,
        IGlobalSearchService globalSearchService,
        ICollectionProgressAnalyticsDashboardQuery collectionProgressAnalyticsDashboardQuery,
        INoteToCardQueueQuery noteToCardQueueQuery,
        IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery,
        IFlashcardCardsQuery flashcardCardsQuery,
        ICreateCardsFromCardContextCommand createCardsFromCardContextCommand,
        IImportFlashcardCollectionCommand importFlashcardCollectionCommand,
        IFlashcardReviewSettingsQuery flashcardReviewSettingsQuery,
        IUpdateFlashcardReviewSettingsCommand updateFlashcardReviewSettingsCommand,
        IThemeService themeService,
        IEditorSettingsService editorSettingsService,
        ILogger<MainViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(documentContentFactory);
        ArgumentNullException.ThrowIfNull(collectionPathResolver);
        ArgumentNullException.ThrowIfNull(knownCollectionStore);
        ArgumentNullException.ThrowIfNull(globalSearchService);
        ArgumentNullException.ThrowIfNull(collectionProgressAnalyticsDashboardQuery);
        ArgumentNullException.ThrowIfNull(noteToCardQueueQuery);
        ArgumentNullException.ThrowIfNull(flashcardDeckSummariesQuery);
        ArgumentNullException.ThrowIfNull(flashcardCardsQuery);
        ArgumentNullException.ThrowIfNull(createCardsFromCardContextCommand);
        ArgumentNullException.ThrowIfNull(importFlashcardCollectionCommand);
        ArgumentNullException.ThrowIfNull(flashcardReviewSettingsQuery);
        ArgumentNullException.ThrowIfNull(updateFlashcardReviewSettingsCommand);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(editorSettingsService);

        this.noteService = noteService;
        this.documentContentFactory = documentContentFactory;
        this.collectionPathResolver = collectionPathResolver;
        this.knownCollectionStore = knownCollectionStore;
        this.globalSearchService = globalSearchService;
        this.collectionProgressAnalyticsDashboardQuery = collectionProgressAnalyticsDashboardQuery;
        this.noteToCardQueueQuery = noteToCardQueueQuery;
        this.flashcardDeckSummariesQuery = flashcardDeckSummariesQuery;
        this.flashcardCardsQuery = flashcardCardsQuery;
        this.createCardsFromCardContextCommand = createCardsFromCardContextCommand;
        this.importFlashcardCollectionCommand = importFlashcardCollectionCommand;
        this.flashcardReviewSettingsQuery = flashcardReviewSettingsQuery;
        this.updateFlashcardReviewSettingsCommand = updateFlashcardReviewSettingsCommand;
        this.themeService = themeService;
        this.editorSettingsService = editorSettingsService;
        this.logger = logger;

        InitializeReviewParameterSettings();
        InitializeThemeOptions();
        InitializeFontFamilyOptions();
        InitializeEditorSettings();
        InitializeCommands();
        InitializeNoteToCardPresentationOptions();
    }

    internal event EventHandler<Navigation.OverlayRequestedEventArgs>? OverlayRequested;

    internal event EventHandler? ClearEditorRequested;

    internal event EventHandler<Navigation.OpenNoteRequestedEventArgs>? OpenNoteRequested;

    internal event Func<object?, Navigation.NoteDocumentsDeletedEventArgs, Task>? NoteDocumentsDeleting;

    internal event EventHandler<Navigation.NoteDocumentsDeletedEventArgs>? NoteDocumentsDeleted;

    internal event EventHandler<Navigation.OpenPdfRequestedEventArgs>? OpenPdfRequested;

    internal event EventHandler? MentionTargetsChanged;

    internal event EventHandler<Navigation.UserMessageRequestedEventArgs>? UserMessageRequested;

    internal event EventHandler<CardCandidatePreviewRequestedEventArgs>? CardCandidatePreviewRequested;

    internal event EventHandler? AddSelectedTextToSelectedCardSlotRequested;

    internal event EventHandler<string>? CurrentNoteTitleChanged;

    internal event EventHandler<string>? CurrentNoteContentChanged;

    internal event EventHandler<string>? CurrentNoteEditorContentReplacementRequested;

    internal event EventHandler<Navigation.OpenFlashcardRequestedEventArgs>? OpenFlashcardRequested;

    internal event EventHandler<bool>? EditorReadOnlyChanged;

    internal event EventHandler? AutoSaveSettingsChanged;

    public Func<string?>? GetCurrentEditorNotePath { get; set; }

    public Func<string>? GetCurrentEditorContent { get; set; }

    public ObservableCollection<NoteTreeNodeViewModel> NoteRoots { get; } = [];

    public ObservableCollection<RecentNoteViewModel> RecentNotes { get; } = [];

    public ObservableCollection<DashboardMetricViewModel> DashboardMetrics { get; } = [];

    public ObservableCollection<DashboardStudyHeatmapCellViewModel> DashboardStudyHeatmapCells { get; } = [];

    public ObservableCollection<DashboardEmptyStateViewModel> DashboardEmptyStates { get; } = [];

    public ObservableCollection<Navigation.EditorBreadcrumbItemViewModel> EditorBreadcrumbItems { get; } = [];

    public ObservableCollection<Navigation.CollectionOptionViewModel> AvailableCollections { get; } = [];

    internal string CollectionDirectoryExtension => collectionPathResolver.CollectionDirectoryExtension;

    public ObservableCollection<GlobalSearchResultReadModel> GlobalSearchResults { get; } = [];

    public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; } = [];

    public ObservableCollection<string> FontFamilyOptions { get; } = [];

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
            double normalized = Math.Clamp(
                Math.Round(value, 0),
                EditorSettings.MinimumGlobalTextFontSize,
                EditorSettings.MaximumGlobalTextFontSize);
            if (SetProperty(ref globalTextFontSize, normalized))
            {
                globalTextFontSizeText = normalized.ToString("0", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(GlobalTextFontSizeText));
                AutoSaveSettingsStatusMessage = string.Empty;
                HandleEditorVisualSettingsChanged();
                RaiseEditorSettingsCommandStates();
            }
        }
    }

    public string GlobalTextFontSizeText
    {
        get => globalTextFontSizeText;
        set
        {
            if (!SetProperty(ref globalTextFontSizeText, value))
            {
                return;
            }

            if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out double fontSize))
            {
                GlobalTextFontSize = fontSize;
                return;
            }

            AutoSaveSettingsStatusMessage = string.Empty;
            HandleEditorVisualSettingsChanged();
            RaiseEditorSettingsCommandStates();
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
            AutoSaveSettingsStatusMessage = string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentThemeDescription));
            OnPropertyChanged(nameof(CurrentThemeName));
            OnPropertyChanged(nameof(IsGraphiteTheme));
            RaiseEditorSettingsCommandStates();
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
