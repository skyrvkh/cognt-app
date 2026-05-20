using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Wpf.EditorHost;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Features.NoteToCard;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Attachments;
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
    private readonly NoteCardQueueView noteCardQueueView = new();
    private readonly SettingsView settingsView = new();
    private readonly MainViewModel viewModel;
    private readonly EditorView EditorViewControl;
    private readonly IEditorViewFactory editorViewFactory;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
    private readonly ILogger<MainWindow> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly StartupWorkflowController startupWorkflow;
    private readonly BreadcrumbCommandController breadcrumbCommands;
    private readonly NoteTreeInteractionController noteTreeInteractions;
    private readonly NoteTreeCommandController noteTreeCommands;
    private readonly IShellDialogService shellDialogs;
    private readonly OverlayHostController overlayHost;
    private readonly OverlayNavigationCoordinator overlayNavigation;
    private readonly ReviewOverlayCoordinator reviewOverlay;
    private readonly Dictionary<string, NoteWorkspaceDocumentSession> openNoteDocuments = new(StringComparer.OrdinalIgnoreCase);
    private readonly NoteDocumentDragController noteDocumentDragController = new();
    private readonly SemaphoreSlim openNoteDocumentGate = new(1, 1);
    private NoteWorkspaceDocumentSession? activeNoteDocument;
    private bool isStartupInitialized;
    private bool lowerRightToolsAutoHideApplied;
    private bool hasSavedOpenDocumentsForClose;
    private bool isSavingOpenDocumentsForClose;

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
        IEditorViewFactory editorViewFactory,
        EditorView editorViewControl,
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
        this.editorViewFactory = editorViewFactory;
        EditorViewControl = editorViewControl;
        this.logger = logger;
        this.loggerFactory = loggerFactory;

        this.logger.LogInformation("Constructing MainWindow.");
        InitializeComponent();
        ApplyShellTheme();
        shellDialogs = new WindowShellDialogService(this);
        attachmentWorkflow = new AttachmentWorkflowController(viewModel, shellDialogs);
        collectionWorkflow = new CollectionWorkflowController(viewModel, shellDialogs);
        InspectorToolHost.Content = noteInspectorView;
        GraphToolHost.Content = noteGraphToolView;
        CardsToolHost.Content = noteCardQueueView;
        PdfToolHost.Content = GetOrCreatePdfToolView();
        rightPaneBrowserView ??= new BrowserPaneView();
        BrowserToolHost.Content = rightPaneBrowserView;
        InspectorAnchorable.Show();
        InspectorAnchorable.IsSelected = true;
        GraphAnchorable.Show();
        CardsAnchorable.Show();
        PdfAnchorable.Show();
        BrowserAnchorable.Show();
        noteInspectorViewModel.Activate();
        overlayHost = new OverlayHostController(OverlayHost, EditorViewControl);
        reviewOverlay = new ReviewOverlayCoordinator(
            overlayHost,
            shellDialogs,
            reviewQueueQuery,
            recordFlashcardReviewCommand,
            cramSessionEngine,
            () => this.viewModel.OpenCollectionPath);
        overlayNavigation = new OverlayNavigationCoordinator(
            shellDialogs,
            this.viewModel,
            this.allNotesViewModel,
            this.graphViewModel,
            dashboardView,
            this.allNotesView,
            this.graphView,
            this.decksView,
            settingsView,
            overlayHost,
            EditorViewControl,
            reviewOverlay);
        startupWorkflow = new StartupWorkflowController(viewModel, shellDialogs);
        breadcrumbCommands = new BreadcrumbCommandController(viewModel, shellDialogs);
        noteTreeInteractions = new NoteTreeInteractionController(
            NotesTree,
            viewModel,
            shellDialogs,
            loggerFactory.CreateLogger<NoteTreeInteractionController>());
        noteTreeCommands = new NoteTreeCommandController(viewModel, shellDialogs);
        BindViewDataContexts();
        RegisterViewEvents();
        RegisterViewModelEvents();
        RegisterWorkspaceDockingEvents();
        this.logger.LogInformation("MainWindow constructed.");
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
            await SaveOpenDocumentsForCloseAsync();
            hasSavedOpenDocumentsForClose = true;
            Close();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save open notes before closing.");
            shellDialogs.ShowWarning("Close Failed", $"Open notes could not be saved before closing: {ex.Message}");
        }
        finally
        {
            isSavingOpenDocumentsForClose = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        logger.LogInformation("MainWindow closing. OpenDocumentCount={OpenDocumentCount}", openNoteDocuments.Count);
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values)
        {
            try
            {
                session.SavePendingChanges();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save note while closing application. NotePath={NotePath}", session.NotePath);
            }

            session.Dispose();
        }

        openNoteDocumentGate.Dispose();
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.Dispose();
        base.OnClosed(e);
    }

    private async Task SaveOpenDocumentsForCloseAsync()
    {
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values.ToArray())
        {
            await session.SavePendingChangesAsync();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("MainWindow loaded.");
        ApplyDesktopNavigation();
        ApplyDefaultLowerRightToolAutoHide();

        if (isStartupInitialized)
        {
            return;
        }

        isStartupInitialized = true;
        await startupWorkflow.InitializeAsync();
    }

    private void CollectionSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.OpenSelector(CollectionSelectorPopup);
    }

    private void CollectionOptionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.SelectCollection(
            (sender as FrameworkElement)?.DataContext as CollectionOptionViewModel,
            CollectionSelectorPopup);
    }

    private void AddCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.CreateCollection(CollectionSelectorPopup);
    }

    private void OpenExistingCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.OpenExistingCollection(CollectionSelectorPopup);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        attachmentWorkflow.ShowDocumentImport(this);
    }

    private async void ImportAttachmentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string importKind = (sender as FrameworkElement)?.Tag as string ?? "Document";
        await attachmentWorkflow.ImportAttachmentAsync(this, importKind);
    }

    private void SidebarSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        viewModel.ShowGlobalSearchPopup();
    }

    private void SidebarSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        viewModel.DismissGlobalSearchPopup();
        e.Handled = true;
    }

    private void EditorViewHost_DragOver(object sender, DragEventArgs e)
    {
        attachmentWorkflow.HandleDragOver(e);
    }

    private async void EditorViewHost_Drop(object sender, DragEventArgs e)
    {
        await attachmentWorkflow.HandleDropAsync(e);
    }

    private void RemoveCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.RemoveCurrentCollection(CollectionSelectorPopup);
    }
}
