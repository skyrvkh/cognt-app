using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CogntApp.Models.Decks;
using CogntApp.Models.Markdown;
using CogntApp.Models.Navigation;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.DocumentImport;
using CogntApp.Shell.Editor;
using CogntApp.Shell.Navigation;
using CogntApp.Shell.Overlay;
using CogntApp.Services;
using CogntApp.ViewModels;
using CogntApp.Views;
using CreateExtractRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateExtractRequestedEventArgs;
using CreateLinkedNoteRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteRequestedEventArgs;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;
using KnowledgeMentionTargetReadModel = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.KnowledgeMentionTargetReadModel;
using MentionPreviewRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionPreviewRequestedEventArgs;
using MentionSearchRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionSearchRequestedEventArgs;
using OpenMentionRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.OpenMentionRequestedEventArgs;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double CollapsedSidebarWidth = 88;
    private const double DesktopSidebarWidth = 300;
    private const double CompactNavigationBreakpoint = 1180;
    private const string NoteTreeDragDataFormat = "CogntApp.NoteTree.ItemId";
    private static readonly Geometry ChevronLeftIcon = CreateFrozenGeometry("M11.5,4 L5.5,10 L11.5,16");
    private static readonly Geometry ChevronRightIcon = CreateFrozenGeometry("M5.5,4 L11.5,10 L5.5,16");
    private static readonly Geometry CloseIcon = CreateFrozenGeometry("M4,4 L14,14 M14,4 L4,14");

    private readonly DashboardView dashboardView = new();
    private readonly DecksView decksView = new();
    private readonly MainViewModel viewModel;
    private readonly AllNotesViewModel allNotesViewModel;
    private readonly IShellDialogService shellDialogs;
    private readonly NoteTreeCommandController noteTreeCommands;
    private readonly NoteTreeInteractionController noteTreeInteractions;
    private readonly OverlayHostController overlayHostController;
    private readonly AllNotesView allNotesView = new();
    private bool isDesktopSidebarCollapsed;
    private bool isCompactNavigationMode;
    private bool isSidebarSheetOpen;
    private ReviewView? reviewView;
    private int editorOpenRequestId;
    private int mentionTargetsRefreshRequestId;
    private NoteTreeNodeViewModel? noteTreeContextNode;
    private bool suppressTreeSelectionHandling;
    private bool isStartupInitialized;

    private enum TreeDropPlacement
    {
        Before,
        After,
        Inside,
    }

    private readonly record struct TreeDropTarget(long? ParentId, long? PreviousSiblingId, long? NextSiblingId);

    public MainWindow()
        : this(new NoteService())
    {
    }

    internal MainWindow(INoteService noteService)
        : this(new MainViewModel(noteService))
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        allNotesViewModel = new AllNotesViewModel(new AllNotesCatalogSource(new NoteService()));

        InitializeComponent();
        DataContext = viewModel;
        allNotesView.DataContext = allNotesViewModel;
        shellDialogs = new WindowShellDialogService(this);
        noteTreeCommands = new NoteTreeCommandController(viewModel, shellDialogs);
        noteTreeInteractions = new NoteTreeInteractionController(NotesTree, viewModel, shellDialogs);
        overlayHostController = new OverlayHostController(OverlayHost, EditorViewControl);

        viewModel.GetCurrentEditorContent = EditorViewControl.GetContent;
        viewModel.OverlayRequested += ViewModel_OverlayRequested;
        viewModel.ClearEditorRequested += ViewModel_ClearEditorRequested;
        viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        viewModel.MentionTargetsChanged += ViewModel_MentionTargetsChanged;
        viewModel.UserMessageRequested += ViewModel_UserMessageRequested;
        viewModel.CurrentNoteTitleChanged += ViewModel_CurrentNoteTitleChanged;

        EditorViewControl.ContentChanged += EditorView_ContentChanged;
        EditorViewControl.CreateExtractRequested += EditorView_CreateExtractRequested;
        EditorViewControl.MentionSearchRequested += EditorView_MentionSearchRequested;
        EditorViewControl.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        EditorViewControl.OpenMentionRequested += EditorView_OpenMentionRequested;
        EditorViewControl.CreateLinkedNoteRequested += EditorView_CreateLinkedNoteRequested;
        allNotesViewModel.OpenNoteRequested += AllNotesViewModel_OpenNoteRequested;
        allNotesViewModel.CreateNoteRequested += AllNotesViewModel_CreateNoteRequested;
        allNotesViewModel.ImportDocumentsRequested += AllNotesViewModel_ImportDocumentsRequested;
        decksView.PracticeRequested += DecksView_PracticeRequested;
        decksView.CreateCardEditorHostRequested += DecksView_CreateCardEditorHostRequested;
        decksView.CreateCardEditorReleased += DecksView_CreateCardEditorReleased;
    }

    protected override void OnClosed(EventArgs e)
    {
        allNotesViewModel.Dispose();
        viewModel.Dispose();
        base.OnClosed(e);
    }

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        noteTreeInteractions.HandleSelectedItemChanged(e.NewValue);

        if (!string.IsNullOrWhiteSpace((e.NewValue as NoteTreeNodeViewModel)?.NotePath))
        {
            CloseSidebarSheet();
        }
    }

    private void NotesTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        noteTreeInteractions.HandleExpanded(e);
    }

    private void NotesTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            RenameSelectedTreeNode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteSelectedTreeNode();
            e.Handled = true;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveNavigation(animate: false);

        if (isStartupInitialized)
        {
            return;
        }

        isStartupInitialized = true;

        try
        {
            EditorViewControl.AttachBlockSuiteHostToEditor();
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            await EditorViewControl.WarmUpAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The editor host could not finish starting: {ex.Message}",
                "Editor Startup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        try
        {
            viewModel.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The collection could not be initialized: {ex.Message}",
                "Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || !e.WidthChanged)
        {
            return;
        }

        ApplyResponsiveNavigation(animate: true);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !isCompactNavigationMode || !isSidebarSheetOpen)
        {
            return;
        }

        CloseSidebarSheet();
        e.Handled = true;
    }

    private void NavigationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isCompactNavigationMode)
        {
            return;
        }

        isSidebarSheetOpen = !isSidebarSheetOpen;
        ApplyResponsiveNavigation(animate: true);
    }

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (isCompactNavigationMode)
        {
            CloseSidebarSheet();
            return;
        }

        isDesktopSidebarCollapsed = !isDesktopSidebarCollapsed;
        ApplyResponsiveNavigation(animate: false);
    }

    private void SidebarBackdrop_Click(object sender, RoutedEventArgs e)
    {
        CloseSidebarSheet();
    }

    private void SidebarActionButton_Click(object sender, RoutedEventArgs e)
    {
        CloseSidebarSheet();
    }

    private void ImportDocumentsButton_Click(object sender, RoutedEventArgs e)
    {
        DocumentImportWindow window = new(new DocumentImportViewModel(viewModel))
        {
            Owner = this
        };
        CloseSidebarSheet();
        window.ShowDialog();
    }

    private void CloseSidebarSheet()
    {
        if (!isCompactNavigationMode || !isSidebarSheetOpen)
        {
            return;
        }

        isSidebarSheetOpen = false;
        ApplyResponsiveNavigation(animate: true);
    }

    private void ApplyResponsiveNavigation(bool animate)
    {
        bool shouldUseCompactNavigation = ActualWidth < CompactNavigationBreakpoint;

        if (shouldUseCompactNavigation != isCompactNavigationMode)
        {
            isCompactNavigationMode = shouldUseCompactNavigation;

            if (!isCompactNavigationMode)
            {
                isSidebarSheetOpen = false;
            }
        }

        bool showExpandedSidebarContent = isCompactNavigationMode || !isDesktopSidebarCollapsed;
        double targetDesktopSidebarWidth = isDesktopSidebarCollapsed
            ? CollapsedSidebarWidth
            : DesktopSidebarWidth;
        double targetSidebarWidth = isCompactNavigationMode
            ? DesktopSidebarWidth
            : targetDesktopSidebarWidth;

        SidebarHost.Width = targetSidebarWidth;
        SidebarSpacerColumn.Width = isCompactNavigationMode
            ? ResponsiveLayout.Zero
            : ResponsiveLayout.Pixel(targetSidebarWidth);
        MobileHeaderRow.Height = isCompactNavigationMode
            ? GridLength.Auto
            : ResponsiveLayout.Zero;
        MobileHeader.Visibility = ResponsiveLayout.VisibleWhen(isCompactNavigationMode);
        SidebarBackdrop.Visibility = ResponsiveLayout.VisibleWhen(isCompactNavigationMode);
        SidebarBackdrop.IsHitTestVisible = isCompactNavigationMode && isSidebarSheetOpen;
        SidebarHost.IsEnabled = !isCompactNavigationMode || isSidebarSheetOpen;
        SidebarHost.IsHitTestVisible = !isCompactNavigationMode || isSidebarSheetOpen;
        SidebarHost.CornerRadius = isCompactNavigationMode
            ? new CornerRadius(0, 18, 18, 0)
            : new CornerRadius(0);
        SidebarShadowEffect.Opacity = isCompactNavigationMode ? 0.45 : 0;
        SidebarHeader.Margin = showExpandedSidebarContent
            ? new Thickness(0, 0, 0, 12)
            : new Thickness(0);
        Visibility expandedSidebarVisibility = ResponsiveLayout.VisibleWhen(showExpandedSidebarContent);
        SidebarBrandText.Visibility = expandedSidebarVisibility;
        SidebarCollectionSection.Visibility = expandedSidebarVisibility;
        SidebarScrollContent.Visibility = expandedSidebarVisibility;
        SidebarFooterSection.Visibility = expandedSidebarVisibility;

        SidebarToggleIcon.Data = isCompactNavigationMode
            ? CloseIcon
            : isDesktopSidebarCollapsed
                ? ChevronRightIcon
                : ChevronLeftIcon;
        SidebarToggleButton.ToolTip = isCompactNavigationMode
            ? "Close navigation"
            : isDesktopSidebarCollapsed
                ? "Show navigation"
                : "Hide navigation";

        double targetSidebarOffset = isCompactNavigationMode
            ? (isSidebarSheetOpen ? 0 : -(DesktopSidebarWidth + 24))
            : 0;
        double targetBackdropOpacity = isCompactNavigationMode && isSidebarSheetOpen
            ? 1
            : 0;

        AnimateDouble(SidebarSheetTransform, TranslateTransform.XProperty, targetSidebarOffset, animate);
        AnimateDouble(SidebarBackdrop, UIElement.OpacityProperty, targetBackdropOpacity, animate);
    }

    private static Geometry CreateFrozenGeometry(string pathData)
    {
        Geometry geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }

    private static void AnimateDouble(
        DependencyObject target,
        DependencyProperty property,
        double value,
        bool animate)
    {
        if (!animate)
        {
            if (target is Animatable nonAnimatedTarget)
            {
                nonAnimatedTarget.BeginAnimation(property, null);
            }

            target.SetValue(property, value);
            return;
        }

        DoubleAnimation animation = new()
        {
            To = value,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
        };

        if (target is Animatable animatable)
        {
            animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            if (target.GetValue(property) is double currentValue
                && Math.Abs(currentValue - value) < 0.1)
            {
                return;
            }

            target.SetValue(property, value);
        }
    }

    private async void ViewModel_OverlayRequested(object? sender, OverlayRequestedEventArgs e)
    {
        try
        {
            editorOpenRequestId++;
            CloseSidebarSheet();

            switch (e.ViewKind)
            {
                case OverlayViewKind.Decks:
                    await CloseReviewIfActiveAsync();
                    ShowOverlay(decksView);
                    return;

                case OverlayViewKind.AllNotes:
                    await CloseReviewIfActiveAsync();
                    await CloseCreateCardEditorIfActiveAsync();
                    await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
                    ShowOverlay(allNotesView);
                    return;

                case OverlayViewKind.Review:
                    try
                    {
                        await CloseCreateCardEditorIfActiveAsync();
                        await ShowReviewOverlayAsync(EnsureReviewView());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Review mode could not start: {ex.Message}",
                            "Review Not Ready",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        await CloseReviewIfActiveAsync();
                        ShowOverlay(decksView);
                    }

                    return;

                default:
                    await CloseReviewIfActiveAsync();
                    await CloseCreateCardEditorIfActiveAsync();
                    ShowOverlay(dashboardView);
                    return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The view could not be changed: {ex.Message}",
                "Navigation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EditorViewControl.AttachBlockSuiteHostToEditor();
        }
    }

    private async void DecksView_PracticeRequested(object? sender, EventArgs e)
    {
        int requestId = ++editorOpenRequestId;

        try
        {
            viewModel.SaveCurrentNoteFromEditor();
            ReviewView review = EnsureReviewView();
            await CloseCreateCardEditorIfActiveAsync();
            await ShowReviewOverlayAsync(review);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"Review mode could not start: {ex.Message}",
                    "Review Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                await CloseReviewIfActiveAsync();
                ShowOverlay(decksView);
            }
        }
    }

    private async void DecksView_CreateCardEditorHostRequested(
        object? sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        int requestId = ++editorOpenRequestId;

        try
        {
            await CloseReviewIfActiveAsync();
            await EditorViewControl.EditTransientNoteInHostAsync(
                e.Host,
                "Card Text",
                e.Content,
                e.ContentChanged);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The card editor could not start: {ex.Message}",
                    "Card Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private async void DecksView_CreateCardEditorReleased(object? sender, EventArgs e)
    {
        try
        {
            await CloseCreateCardEditorIfActiveAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Card editor restore failed: {ex}");
            EditorViewControl.AttachBlockSuiteHostToEditor();
        }
    }

    private async void ReviewView_ExitReviewRequested(object? sender, EventArgs e)
    {
        try
        {
            editorOpenRequestId++;
            await CloseReviewIfActiveAsync();
            ShowOverlay(decksView);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Review mode could not close cleanly: {ex.Message}",
                "Review",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EditorViewControl.AttachBlockSuiteHostToEditor();
            ShowOverlay(decksView);
        }
    }

    private async void ViewModel_ClearEditorRequested(object? sender, EventArgs e)
    {
        await CreateEditorRequestController().HandleClearEditorRequestedAsync();
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        await CreateEditorRequestController().HandleOpenNoteRequestedAsync(e);
    }

    private void ViewModel_MentionTargetsChanged(object? sender, EventArgs e)
    {
        CreateEditorRequestController().HandleMentionTargetsChanged();
    }

    private void ViewModel_UserMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        CreateEditorRequestController().HandleUserMessageRequested(e);
    }

    private async void AllNotesViewModel_OpenNoteRequested(object? sender, OpenAllNotesRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath))
        {
            return;
        }

        string notePath = Path.GetFullPath(Path.Combine(
            viewModel.OpenCollectionPath,
            e.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        await OpenNoteAsync(notePath, selectInTree: true);
    }

    private void AllNotesViewModel_CreateNoteRequested(object? sender, EventArgs e)
    {
        if (viewModel.AddNoteCommand.CanExecute(null))
        {
            viewModel.AddNoteCommand.Execute(null);
        }
    }

    private void AllNotesViewModel_ImportDocumentsRequested(object? sender, EventArgs e)
    {
        ImportDocumentsButton_Click(sender ?? this, new RoutedEventArgs());
    }

    private void ViewModel_CurrentNoteTitleChanged(object? sender, string title)
    {
        CreateEditorRequestController().HandleCurrentNoteTitleChanged(title);
    }

    private EditorRequestController CreateEditorRequestController()
    {
        return new EditorRequestController(
            ShowEmptyEditorViewAsync,
            OpenNoteAsync,
            QueueMentionTargetsRefresh,
            EditorViewControl.UpdateCurrentNoteTitle,
            new WindowShellDialogService(this));
    }

    private async Task<bool> OpenNoteAsync(string notePath, bool selectInTree)
    {
        PreparedNote? note;

        try
        {
            note = viewModel.PrepareNoteForOpen(notePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The note could not be opened: {ex.Message}",
                "Open Note Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (note is null)
        {
            return true;
        }

        int requestId = ++editorOpenRequestId;
        await CloseReviewIfActiveAsync();

        try
        {
            EditorViewControl.AttachBlockSuiteHostToEditor();
            await EditorViewControl.LoadNoteAsync(
                note.Title,
                note.Content,
                note.NotePath);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The editor could not open this note: {ex.Message}",
                    "Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }

        if (requestId != editorOpenRequestId)
        {
            return false;
        }

        viewModel.CompleteOpenNote(note);
        HideOverlay();

        if (selectInTree)
        {
            SelectNote(note.NotePath);
        }

        return true;
    }

    private async Task ShowEmptyEditorViewAsync()
    {
        int requestId = ++editorOpenRequestId;
        await CloseReviewIfActiveAsync();
        HideOverlay();

        try
        {
            EditorViewControl.AttachBlockSuiteHostToEditor();
            await EditorViewControl.ClearNoteAsync();
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The editor could not be cleared: {ex.Message}",
                    "Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        string? sourcePath = e.SourcePath;

        try
        {
            string createdPath = viewModel.CreateExtractNote(sourcePath, e.SelectedContent, e.SelectedPlainText);
            EditorViewControl.PostExtractResult(sourcePath, ok: true, createdPath);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostExtractResult(sourcePath, ok: false, error: ex.Message);
        }
    }

    private void EditorView_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        e.Complete(viewModel.SearchMentionTargets(e.Query, e.Limit).Select(ToKnowledgeMentionTarget));
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        try
        {
            MentionPreview preview = viewModel.ReadMentionPreview(e.RelativePath, e.MaxCharacters);

            EditorViewControl.PostMentionPreviewResult(
                e.RequestId,
                preview.RelativePath,
                e.Mode,
                ok: true,
                title: preview.Title,
                documentContent: preview.DocumentContent);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: ex.Message);
        }
    }

    private async void EditorView_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        try
        {
            viewModel.SaveMentionSource(e.SourcePath, e.Content);
            string targetPath = viewModel.ResolveMentionPath(e.RelativePath);
            bool opened = await OpenNoteAsync(targetPath, selectInTree: true);
            EditorViewControl.PostMentionOpenResult(e.RelativePath, opened, opened ? null : "The mentioned note could not be opened.");
        }
        catch (Exception ex)
        {
            EditorViewControl.PostMentionOpenResult(e.RelativePath, ok: false, error: ex.Message);
        }
    }

    private void EditorView_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        try
        {
            LinkedNote note = viewModel.CreateLinkedNote(e.SourcePath, e.ViewMode);
            e.Complete(CreateLinkedNoteResult.Success(note.RelativePath, note.Title, note.DocumentContent));
        }
        catch (Exception ex)
        {
            e.Complete(CreateLinkedNoteResult.Fail(ex.Message));
        }
    }

    private static KnowledgeMentionTargetReadModel ToKnowledgeMentionTarget(MentionTarget target)
    {
        return new KnowledgeMentionTargetReadModel(target.RelativePath, target.Title);
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        try
        {
            viewModel.SaveNoteIfChanged(content);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostSaveResult(viewModel.CurrentNotePath, ok: false, error: ex.Message);
        }
    }

    private void NotesTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindTreeViewItem(e.OriginalSource) is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
            noteTreeContextNode = item.DataContext as NoteTreeNodeViewModel;
            return;
        }

        noteTreeContextNode = null;
    }

    private void NewFolderTreeItem_Click(object sender, RoutedEventArgs e)
    {
        CreateFolderInTree(noteTreeContextNode);
    }

    private void DeleteTreeItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteTreeNode(noteTreeContextNode ?? (NotesTree.SelectedItem as NoteTreeNodeViewModel));
    }

    private void RenameSelectedTreeNode()
    {
        BeginInlineRename(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void NotesTree_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem item
            || item.DataContext is not NoteTreeNodeViewModel node)
        {
            return;
        }

        item.IsSelected = true;
        item.Focus();
        e.Handled = true;
        BeginInlineRename(node);
    }

    private void DeleteSelectedTreeNode()
    {
        DeleteTreeNode(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void CreateFolderInTree(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.CreateFolder(node, BeginInlineRenameById);
    }

    private void BeginInlineRename(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.BeginInlineRename(node);
    }

    private void BeginInlineRenameById(string itemId)
    {
        if (!long.TryParse(itemId, out long id))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                foreach (TreeViewItem item in EnumerateTreeItems(NotesTree))
                {
                    if (item.DataContext is NoteTreeNodeViewModel node && node.Id == id)
                    {
                        item.IsSelected = true;
                        item.Focus();
                        BeginInlineRename(node);
                        return;
                    }
                }
            },
            DispatcherPriority.Background);
    }

    private void CommitInlineRename(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.CommitInlineRename(node, BeginInlineRename);
    }

    private void TreeRenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || e.NewValue is not true)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                textBox.Focus();
                textBox.SelectAll();
            },
            DispatcherPriority.Input);
    }

    private void TreeRenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || textBox.DataContext is not NoteTreeNodeViewModel node)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitInlineRename(node);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            node.CancelRename();
            e.Handled = true;
        }
    }

    private void TreeRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: NoteTreeNodeViewModel node })
        {
            CommitInlineRename(node);
        }
    }

    private string? ShowRenamePrompt(string itemKind, string currentName)
    {
        TextBlock promptTextBlock = new()
        {
            Text = $"New {itemKind} name",
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1F2937")!,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8),
        };

        TextBox nameTextBox = new()
        {
            Text = currentName,
            Height = 30,
            Padding = new Thickness(8, 3, 8, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        Button cancelButton = new()
        {
            Content = "Cancel",
            Width = 76,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
        };

        Button renameButton = new()
        {
            Content = "Rename",
            Width = 76,
            Height = 28,
            IsDefault = true,
        };

        StackPanel actionsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };
        actionsPanel.Children.Add(cancelButton);
        actionsPanel.Children.Add(renameButton);

        Grid dialogContent = new()
        {
            Margin = new Thickness(16),
        };
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(promptTextBlock, 0);
        Grid.SetRow(nameTextBox, 1);
        Grid.SetRow(actionsPanel, 2);
        dialogContent.Children.Add(promptTextBlock);
        dialogContent.Children.Add(nameTextBox);
        dialogContent.Children.Add(actionsPanel);

        Window dialog = new()
        {
            Title = $"Rename {itemKind}",
            Height = 150,
            Width = 360,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White,
            Owner = this,
            Content = dialogContent,
        };

        dialog.Loaded += (_, _) =>
        {
            nameTextBox.Focus();
            nameTextBox.SelectAll();
        };
        renameButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show(
                    "Name is required.",
                    "Rename",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            dialog.DialogResult = true;
        };

        return dialog.ShowDialog() == true ? nameTextBox.Text : null;
    }

    private void DeleteTreeNode(NoteTreeNodeViewModel? node)
    {
        noteTreeCommands.DeleteNode(node);
    }

    private void NotesTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        noteTreeInteractions.PreviewMouseLeftButtonDown(e, BeginInlineRename);
    }

    private void NotesTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        noteTreeInteractions.PreviewMouseMove(e);
    }

    private void NotesTree_DragOver(object sender, DragEventArgs e)
    {
        noteTreeInteractions.DragOver(e);
    }

    private void NotesTree_Drop(object sender, DragEventArgs e)
    {
        noteTreeInteractions.Drop(e);
    }

    private void RunWithSuppressedTreeSelection(Action action, bool deferRestore = false)
    {
        bool wasSuppressed = suppressTreeSelectionHandling;
        suppressTreeSelectionHandling = true;

        try
        {
            action();
        }
        finally
        {
            if (deferRestore)
            {
                Dispatcher.BeginInvoke(
                    () => suppressTreeSelectionHandling = wasSuppressed,
                    DispatcherPriority.Background);
            }
            else
            {
                suppressTreeSelectionHandling = wasSuppressed;
            }
        }
    }

    private static TreeViewItem? FindTreeViewItem(object? source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null)
        {
            if (current is TreeViewItem item)
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private bool TryGetTreeDropTarget(
        DragEventArgs e,
        out long itemId,
        out TreeDropTarget target)
    {
        itemId = 0;
        target = default;

        if (!e.Data.GetDataPresent(NoteTreeDragDataFormat)
            || e.Data.GetData(NoteTreeDragDataFormat) is not long draggedItemId
            || draggedItemId <= 0)
        {
            return false;
        }

        itemId = draggedItemId;

        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem targetItem)
        {
            target = new TreeDropTarget(ParentId: null, PreviousSiblingId: null, NextSiblingId: null);
            return !string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath);
        }

        if (targetItem.DataContext is not NoteTreeNodeViewModel targetNode
            || targetNode.IsPlaceholder
            || draggedItemId == targetNode.Id)
        {
            return false;
        }

        TreeDropPlacement placement = GetDropPlacement(targetItem, targetNode, e);

        if (placement == TreeDropPlacement.Inside)
        {
            target = new TreeDropTarget(targetNode.Id, PreviousSiblingId: null, NextSiblingId: null);
            return true;
        }

        long? parentId = targetNode.ParentId;
        long? previousSiblingId = placement == TreeDropPlacement.Before
            ? GetPreviousSiblingId(targetNode, draggedItemId)
            : targetNode.Id;
        long? nextSiblingId = placement == TreeDropPlacement.Before
            ? targetNode.Id
            : GetNextSiblingId(targetNode, draggedItemId);

        target = new TreeDropTarget(parentId, previousSiblingId, nextSiblingId);
        return true;
    }

    private static TreeDropPlacement GetDropPlacement(
        TreeViewItem item,
        NoteTreeNodeViewModel targetNode,
        DragEventArgs e)
    {
        double height = Math.Max(1, item.ActualHeight);
        double y = e.GetPosition(item).Y;

        if (targetNode.CanContainChildren && y > height * 0.25 && y < height * 0.75)
        {
            return TreeDropPlacement.Inside;
        }

        return y < height / 2
            ? TreeDropPlacement.Before
            : TreeDropPlacement.After;
    }

    private long? GetPreviousSiblingId(NoteTreeNodeViewModel targetNode, long draggedItemId)
    {
        IReadOnlyList<NoteTreeNodeViewModel>? siblings = FindSiblingList(viewModel.NoteRoots, targetNode);
        if (siblings is null)
        {
            return null;
        }

        for (int index = 0; index < siblings.Count; index++)
        {
            if (!ReferenceEquals(siblings[index], targetNode))
            {
                continue;
            }

            for (int siblingIndex = index - 1; siblingIndex >= 0; siblingIndex--)
            {
                NoteTreeNodeViewModel candidate = siblings[siblingIndex];
                if (!candidate.IsPlaceholder && candidate.Id != draggedItemId)
                {
                    return candidate.Id;
                }
            }

            return null;
        }

        return null;
    }

    private long? GetNextSiblingId(NoteTreeNodeViewModel targetNode, long draggedItemId)
    {
        IReadOnlyList<NoteTreeNodeViewModel>? siblings = FindSiblingList(viewModel.NoteRoots, targetNode);
        if (siblings is null)
        {
            return null;
        }

        for (int index = 0; index < siblings.Count; index++)
        {
            if (!ReferenceEquals(siblings[index], targetNode))
            {
                continue;
            }

            for (int siblingIndex = index + 1; siblingIndex < siblings.Count; siblingIndex++)
            {
                NoteTreeNodeViewModel candidate = siblings[siblingIndex];
                if (!candidate.IsPlaceholder && candidate.Id != draggedItemId)
                {
                    return candidate.Id;
                }
            }

            return null;
        }

        return null;
    }

    private static IReadOnlyList<NoteTreeNodeViewModel>? FindSiblingList(
        IReadOnlyList<NoteTreeNodeViewModel> nodes,
        NoteTreeNodeViewModel targetNode)
    {
        if (nodes.Contains(targetNode))
        {
            return nodes;
        }

        foreach (NoteTreeNodeViewModel node in nodes)
        {
            IReadOnlyList<NoteTreeNodeViewModel>? childResult = FindSiblingList(node.Children, targetNode);
            if (childResult is not null)
            {
                return childResult;
            }
        }

        return null;
    }

    private void SelectNote(string notePath)
    {
        foreach (TreeViewItem item in EnumerateTreeItems(NotesTree))
        {
            if (item.DataContext is NoteTreeNodeViewModel { NotePath: string itemNotePath }
                && string.Equals(itemNotePath, notePath, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
                item.Focus();
                return;
            }
        }
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(ItemsControl parent)
    {
        parent.UpdateLayout();

        foreach (object dataItem in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(dataItem) is not TreeViewItem item)
            {
                continue;
            }

            yield return item;

            foreach (TreeViewItem child in EnumerateTreeItems(item))
            {
                yield return child;
            }
        }
    }

    private async void QueueMentionTargetsRefresh()
    {
        int requestId = ++mentionTargetsRefreshRequestId;

        try
        {
            IReadOnlyList<MentionTarget> targets = await Task.Run(viewModel.ListMentionTargets);

            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            EditorViewControl.PostMentionTargets(targets);
        }
        catch (Exception ex)
        {
            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            MessageBox.Show(
                $"Mention targets could not be refreshed: {ex.Message}",
                "Mentions",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private ReviewView EnsureReviewView()
    {
        if (reviewView is null)
        {
            reviewView = new ReviewView();
            reviewView.ExitReviewRequested += ReviewView_ExitReviewRequested;
            reviewView.RenderCardAsync = RenderReviewCardAsync;
        }

        return reviewView;
    }

    private async Task RenderReviewCardAsync(string content)
    {
        if (reviewView is null)
        {
            throw new InvalidOperationException("Review mode is not active.");
        }

        await EditorViewControl.RenderCardInHostAsync(reviewView.CardRendererHostControl, content);
    }

    private async Task ShowReviewOverlayAsync(ReviewView review)
    {
        ShowOverlay(review);
        await review.StartReviewAsync();
    }

    private async Task CloseReviewIfActiveAsync()
    {
        if (reviewView is null)
        {
            return;
        }

        ReviewView view = reviewView;
        reviewView = null;

        overlayHostController.ClearIfContent(view);

        try
        {
            await EditorViewControl.RestoreNoteAfterCardPreviewAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shared editor restore after review failed: {ex}");
            EditorViewControl.AttachBlockSuiteHostToEditor();
        }

        view.ExitReviewRequested -= ReviewView_ExitReviewRequested;
        view.DisposeReview();
    }

    private Task CloseCreateCardEditorIfActiveAsync()
    {
        return EditorViewControl.IsShowingTransientEditor
            ? EditorViewControl.RestoreNoteAfterTransientEditorAsync()
            : Task.CompletedTask;
    }

    private void ShowOverlay(UIElement overlay)
    {
        overlayHostController.Show(overlay);
    }

    private void HideOverlay()
    {
        overlayHostController.Hide();
    }
}
