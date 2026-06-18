using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Wpf.EditorHost;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Features.NoteToCard;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Diagnostics;
using CogntApp.Shell.Attachments;
using CogntApp.Shell.Browser;
using CogntApp.Shell.Collections;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Editor;
using CogntApp.Shell.Navigation;
using CogntApp.Shell.Overlay;
using CogntApp.Shell.Review;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;
using Microsoft.Extensions.Logging;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DashboardView dashboardView = new();
    private readonly AllNotesView allNotesView;
    private readonly AllNotesViewModel allNotesViewModel;
    private readonly GraphView graphView;
    private readonly GraphViewModel graphViewModel;
    private readonly NoteInspectorView noteInspectorView;
    private readonly NoteGraphToolView noteGraphToolView;
    private readonly NoteInspectorViewModel noteInspectorViewModel;
    private readonly AttachmentWorkflowController attachmentWorkflow;
    private readonly CollectionWorkflowController collectionWorkflow;
    private readonly DecksView decksView;
    private readonly NoteCardQueueView noteCardQueueView;
    private readonly SettingsView settingsView = new();
    private readonly MainViewModel viewModel;
    private readonly EditorView EditorViewControl;
    private readonly IEditorViewFactory editorViewFactory;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
    private readonly IFlashcardCardsQuery flashcardCardsQuery;
    private readonly IUpdateFlashcardContentCommand updateFlashcardContentCommand;
    private readonly ILogger<MainWindow> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly CollectionBrowserEnvironmentProvider browserEnvironmentProvider;
    private readonly CollectionBrowserStateService browserStateService;
    private readonly IWorkspaceSessionStore workspaceSessionStore;
    private readonly StartupWorkflowController startupWorkflow;
    private readonly BreadcrumbCommandController breadcrumbCommands;
    private readonly NoteTreeInteractionController noteTreeInteractions;
    private readonly NoteTreeCommandController noteTreeCommands;
    private readonly IShellDialogService shellDialogs;
    private readonly OverlayHostController overlayHost;
    private readonly OverlayNavigationCoordinator overlayNavigation;
    private readonly ReviewOverlayCoordinator reviewOverlay;
    private readonly Dictionary<string, NoteWorkspaceDocumentSession> openNoteDocuments = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, FlashcardWorkspaceDocumentSession> openFlashcardDocuments = new();
    private readonly NoteDocumentDragController noteDocumentDragController;
    private readonly SemaphoreSlim openNoteDocumentGate = new(1, 1);
    private readonly Dictionary<string, EditorCaretState?> pendingNoteCaretRestoreStates = new(StringComparer.OrdinalIgnoreCase);
    private NoteWorkspaceDocumentSession? activeNoteDocument;
    private NoteWorkspaceDocumentSession? openingNoteDocument;
    private int noteOpenRequestVersion;
    private bool isStartupInitialized;
    private bool hasSavedOpenDocumentsForClose;
    private bool isSavingOpenDocumentsForClose;
    private bool isRestoringWorkspaceSession;
    private bool isWorkspaceSessionRestoreInterrupted;
    private bool isSavingWorkspaceSession;
    private bool isWorkspaceSessionSaveQueued;

    public MainWindow(
        MainViewModel viewModel,
        AllNotesViewModel allNotesViewModel,
        AllNotesView allNotesView,
        GraphViewModel graphViewModel,
        GraphView graphView,
        NoteInspectorViewModel noteInspectorViewModel,
        NoteInspectorView noteInspectorView,
        NoteGraphToolView noteGraphToolView,
        IFlashcardReviewQueueQuery reviewQueueQuery,
        IRecordFlashcardReviewCommand recordFlashcardReviewCommand,
        ICramSessionEngine cramSessionEngine,
        DecksView decksView,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        IFlashcardCardsQuery flashcardCardsQuery,
        IUpdateFlashcardContentCommand updateFlashcardContentCommand,
        IEditorViewFactory editorViewFactory,
        EditorView editorViewControl,
        IWorkspaceSessionStore workspaceSessionStore,
        CollectionBrowserEnvironmentProvider browserEnvironmentProvider,
        CollectionBrowserStateService browserStateService,
        ILogger<MainWindow> logger,
        ILoggerFactory loggerFactory)
    {
        this.viewModel = viewModel;
        this.allNotesViewModel = allNotesViewModel;
        this.allNotesView = allNotesView;
        this.graphViewModel = graphViewModel;
        this.graphView = graphView;
        this.noteInspectorViewModel = noteInspectorViewModel;
        this.noteInspectorView = noteInspectorView;
        this.noteGraphToolView = noteGraphToolView;
        this.decksView = decksView;
        this.imageMaskEditorDialogs = imageMaskEditorDialogs;
        this.flashcardCardsQuery = flashcardCardsQuery;
        this.updateFlashcardContentCommand = updateFlashcardContentCommand;
        this.editorViewFactory = editorViewFactory;
        EditorViewControl = editorViewControl;
        this.workspaceSessionStore = workspaceSessionStore;
        this.browserEnvironmentProvider = browserEnvironmentProvider;
        this.browserStateService = browserStateService;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        noteCardQueueView = new NoteCardQueueView();
        noteDocumentDragController = new NoteDocumentDragController();

        InitializeComponent();
        ApplyShellTheme();
        shellDialogs = new WindowShellDialogService(
            this,
            loggerFactory.CreateLogger<WindowShellDialogService>());
        attachmentWorkflow = new AttachmentWorkflowController(
            viewModel,
            shellDialogs,
            loggerFactory.CreateLogger<AttachmentWorkflowController>(),
            loggerFactory);
        collectionWorkflow = new CollectionWorkflowController(
            viewModel,
            shellDialogs);
        InspectorToolHost.Content = noteInspectorView;
        GraphToolHost.Content = noteGraphToolView;
        CardsToolHost.Content = noteCardQueueView;
        rightPaneBrowserView ??= new BrowserPaneView(
            browserEnvironmentProvider,
            browserStateService,
            () => viewModel.OpenCollectionPath,
            loggerFactory.CreateLogger<BrowserPaneView>(),
            viewModel.CreateBrowserPageNoteAsync,
            shellDialogs);
        BrowserToolHost.Content = rightPaneBrowserView;
        InspectorAnchorable.Show();
        InspectorAnchorable.IsSelected = true;
        GraphAnchorable.Show();
        CardsAnchorable.Show();
        BrowserAnchorable.Show();
        noteInspectorViewModel.Activate();
        ShellCommandRouting commandRouting = CreateShellCommandRouting(
            reviewQueueQuery,
            recordFlashcardReviewCommand,
            cramSessionEngine);
        overlayHost = commandRouting.OverlayHost;
        overlayNavigation = commandRouting.OverlayNavigation;
        reviewOverlay = commandRouting.ReviewOverlay;
        startupWorkflow = commandRouting.StartupWorkflow;
        breadcrumbCommands = commandRouting.BreadcrumbCommands;
        noteTreeInteractions = commandRouting.NoteTreeInteractions;
        noteTreeCommands = commandRouting.NoteTreeCommands;
        BindViewDataContexts();
        RegisterViewEvents();
        RegisterViewModelEvents();
        RegisterWorkspaceDockingEvents();
        viewModel.GetCurrentEditorNotePath = GetActiveWorkspaceEditorNotePath;
        viewModel.GetCurrentEditorContent = GetActiveWorkspaceEditorContent;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || hasSavedOpenDocumentsForClose || isSavingOpenDocumentsForClose)
        {
            return;
        }

        e.Cancel = true;
        isSavingOpenDocumentsForClose = true;

        try
        {
            await SaveWorkspaceSessionAsync();
            if (viewModel.CanSaveOnClose)
            {
                await SaveOpenDocumentsForCloseAsync();
            }

            hasSavedOpenDocumentsForClose = true;
            _ = Dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        Close();
                    }
                    catch (Exception closeException)
                    {
                        CrashSafeLog.RecordFatal("Deferred MainWindow close failed after saving open documents.", closeException);
                        logger.LogError(closeException, "Deferred MainWindow close failed after saving open documents.");
                        Application.Current.Shutdown(1);
                    }
                },
                DispatcherPriority.ApplicationIdle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save open notes before closing.");
            ReportCloseSaveFailure(ex);
        }
        finally
        {
            isSavingOpenDocumentsForClose = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values.ToArray())
        {
            try
            {
                if (viewModel.CanSaveOnClose)
                {
                    session.SavePendingChanges();
                }

                session.Dispose();
            }
            catch (Exception ex)
            {
                CrashSafeLog.RecordFatal(
                    $"Failed to dispose note session during application close. NotePath={session.NotePath}",
                    ex);
                logger.LogError(ex, "Failed to dispose note session while closing application. NotePath={NotePath}", session.NotePath);
            }
        }

        foreach (FlashcardWorkspaceDocumentSession session in openFlashcardDocuments.Values.ToArray())
        {
            try
            {
                session.Dispose();
            }
            catch (Exception ex)
            {
                CrashSafeLog.RecordFatal(
                    $"Failed to dispose flashcard session during application close. CardId={session.CardId}",
                    ex);
                logger.LogError(ex, "Failed to dispose flashcard session while closing application. CardId={CardId}", session.CardId);
            }
        }

        try
        {
            openNoteDocumentGate.Dispose();
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.Dispose();
        }
        catch (Exception ex)
        {
            CrashSafeLog.RecordFatal("Failed to dispose MainWindow services during application close.", ex);
            logger.LogError(ex, "Failed to dispose MainWindow services during application close.");
        }

        base.OnClosed(e);
    }

    private async Task SaveOpenDocumentsForCloseAsync()
    {
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values.ToArray())
        {
            await session.SavePendingChangesAsync();
        }

        foreach (FlashcardWorkspaceDocumentSession session in openFlashcardDocuments.Values.ToArray())
        {
            await session.SavePendingChangesAsync();
        }
    }

    private void ReportCloseSaveFailure(Exception exception)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible || hasSavedOpenDocumentsForClose)
            {
                return;
            }

            try
            {
                shellDialogs.ShowWarning(
                    "Close Failed",
                    $"Open notes could not be saved before closing: {exception.Message}");
            }
            catch (InvalidOperationException dialogException)
            {
                logger.LogWarning(
                    dialogException,
                    "Could not show close failure warning because the window was not in a valid state.");
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDesktopNavigation();
        ApplyDefaultLowerRightToolAutoHide();

        if (isStartupInitialized)
        {
            return;
        }

        isStartupInitialized = true;
        await startupWorkflow.InitializeAsync();
        await RestoreWorkspaceSessionForCurrentCollectionAsync();
    }

}
