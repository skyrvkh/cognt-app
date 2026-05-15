using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
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

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DashboardView dashboardView = new();
    private readonly AllNotesView allNotesView;
    private readonly AllNotesViewModel allNotesViewModel;
    private readonly AttachmentWorkflowController attachmentWorkflow;
    private readonly CollectionWorkflowController collectionWorkflow;
    private readonly DecksView decksView;
    private readonly NoteCardQueueView noteCardQueueView = new();
    private readonly SettingsView settingsView = new();
    private readonly MainViewModel viewModel;
    private readonly EditorView EditorViewControl;
    private readonly IEditorViewFactory editorViewFactory;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
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
    private NoteWorkspaceDocumentSession? activeNoteDocument;
    private bool isStartupInitialized;
    private bool lowerRightToolsAutoHideApplied;

    public MainWindow(
        MainViewModel viewModel,
        AllNotesViewModel allNotesViewModel,
        AllNotesView allNotesView,
        IFlashcardReviewQueueQuery reviewQueueQuery,
        IRecordFlashcardReviewCommand recordFlashcardReviewCommand,
        DecksView decksView,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        IEditorViewFactory editorViewFactory,
        EditorView editorViewControl)
    {
        this.viewModel = viewModel;
        this.allNotesViewModel = allNotesViewModel;
        this.allNotesView = allNotesView;
        this.decksView = decksView;
        this.imageMaskEditorDialogs = imageMaskEditorDialogs;
        this.editorViewFactory = editorViewFactory;
        EditorViewControl = editorViewControl;

        InitializeComponent();
        shellDialogs = new WindowShellDialogService(this);
        attachmentWorkflow = new AttachmentWorkflowController(viewModel, shellDialogs);
        collectionWorkflow = new CollectionWorkflowController(viewModel, shellDialogs);
        CardsToolHost.Content = noteCardQueueView;
        PdfToolHost.Content = GetOrCreatePdfToolView();
        rightPaneBrowserView ??= new BrowserPaneView();
        BrowserToolHost.Content = rightPaneBrowserView;
        CardsAnchorable.Show();
        PdfAnchorable.Show();
        BrowserAnchorable.Show();
        overlayHost = new OverlayHostController(OverlayHost, EditorViewControl);
        reviewOverlay = new ReviewOverlayCoordinator(
            overlayHost,
            shellDialogs,
            reviewQueueQuery,
            recordFlashcardReviewCommand,
            () => this.viewModel.OpenCollectionPath);
        overlayNavigation = new OverlayNavigationCoordinator(
            shellDialogs,
            this.viewModel,
            this.allNotesViewModel,
            dashboardView,
            this.allNotesView,
            this.decksView,
            settingsView,
            overlayHost,
            EditorViewControl,
            reviewOverlay);
        startupWorkflow = new StartupWorkflowController(viewModel, shellDialogs);
        breadcrumbCommands = new BreadcrumbCommandController(viewModel, shellDialogs);
        noteTreeInteractions = new NoteTreeInteractionController(NotesTree, viewModel, shellDialogs);
        noteTreeCommands = new NoteTreeCommandController(viewModel, shellDialogs);
        BindViewDataContexts();
        RegisterViewEvents();
        RegisterViewModelEvents();
        RegisterWorkspaceDockingEvents();
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values)
        {
            session.Dispose();
        }

        viewModel.Dispose();
        base.OnClosed(e);
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
