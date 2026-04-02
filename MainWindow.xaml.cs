using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CogntApp.Models.Decks;
using CogntApp.Models.Editor;
using CogntApp.Models.Markdown;
using CogntApp.Models.Navigation;
using CogntApp.Services;
using CogntApp.ViewModels;
using CogntApp.Views;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double CollapsedSidebarWidth = 88;
    private const double DesktopSidebarWidth = 300;
    private const double CompactNavigationBreakpoint = 1180;
    private const string NoteTreeDragDataFormat = "CogntApp.NoteTree.MarkdownPath";
    private static readonly Geometry ChevronLeftIcon = CreateFrozenGeometry("M11.5,4 L5.5,10 L11.5,16");
    private static readonly Geometry ChevronRightIcon = CreateFrozenGeometry("M5.5,4 L11.5,10 L5.5,16");
    private static readonly Geometry CloseIcon = CreateFrozenGeometry("M4,4 L14,14 M14,4 L4,14");

    private readonly DashboardView dashboardView = new();
    private readonly DecksView decksView = new();
    private readonly MainViewModel viewModel;
    private bool isDesktopSidebarCollapsed;
    private bool isCompactNavigationMode;
    private bool isSidebarSheetOpen;
    private ReviewView? reviewView;
    private int editorOpenRequestId;

    public MainWindow()
        : this(new MarkdownNoteService())
    {
    }

    internal MainWindow(IMarkdownNoteService noteService)
        : this(new MainViewModel(noteService))
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;

        InitializeComponent();
        DataContext = viewModel;

        viewModel.GetCurrentEditorContent = EditorViewControl.GetContent;
        viewModel.OverlayRequested += ViewModel_OverlayRequested;
        viewModel.ClearEditorRequested += ViewModel_ClearEditorRequested;
        viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        viewModel.NoteSelectionRequested += ViewModel_NoteSelectionRequested;
        viewModel.MentionTargetsChanged += ViewModel_MentionTargetsChanged;
        viewModel.UserMessageRequested += ViewModel_UserMessageRequested;

        EditorViewControl.ContentChanged += EditorView_ContentChanged;
        EditorViewControl.CreateExtractRequested += EditorView_CreateExtractRequested;
        EditorViewControl.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        EditorViewControl.OpenMentionRequested += EditorView_OpenMentionRequested;
        decksView.PracticeRequested += DecksView_PracticeRequested;
        decksView.CreateCardEditorHostRequested += DecksView_CreateCardEditorHostRequested;
        decksView.CreateCardEditorReleased += DecksView_CreateCardEditorReleased;

        viewModel.Initialize();
        _ = EditorViewControl.WarmUpAsync();
    }

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (viewModel.IsLoadingNotesDirectory)
        {
            return;
        }

        NoteTreeNodeViewModel? selectedNode = e.NewValue as NoteTreeNodeViewModel;
        try
        {
            viewModel.SelectTreeNode(selectedNode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The selected note could not be opened: {ex.Message}",
                "Open Note Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedNode?.NotePath))
        {
            CloseSidebarSheet();
        }
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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveNavigation(animate: false);
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
                e.Markdown,
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
        try
        {
            await ShowEmptyEditorViewAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The editor could not be cleared: {ex.Message}",
                "Editor Not Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        try
        {
            await OpenNoteAsync(e.NotePath, e.SelectInTree);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The note could not be opened: {ex.Message}",
                "Open Note Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ViewModel_NoteSelectionRequested(object? sender, NoteSelectionRequestedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            () => SelectNote(e.NotePath),
            DispatcherPriority.Background);
    }

    private void ViewModel_MentionTargetsChanged(object? sender, EventArgs e)
    {
        SendMentionTargets();
    }

    private void ViewModel_UserMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        MessageBox.Show(
            e.Message,
            e.Title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
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
            await EditorViewControl.LoadNoteAsync(note.Title, note.Content, note.NotePath);
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
            string createdPath = viewModel.CreateExtractFile(sourcePath, e.SelectedMarkdown);
            EditorViewControl.PostExtractResult(sourcePath, ok: true, createdPath);
        }
        catch (Exception ex)
        {
            EditorViewControl.PostExtractResult(sourcePath, ok: false, error: ex.Message);
        }
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        try
        {
            MarkdownMentionPreview preview = viewModel.ReadMentionPreview(e.RelativePath, e.MaxCharacters);

            EditorViewControl.PostMentionPreviewResult(
                e.RequestId,
                preview.RelativePath,
                e.Mode,
                ok: true,
                title: preview.Title,
                markdownExcerpt: preview.MarkdownExcerpt);
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
            viewModel.SaveMentionSource(e.SourcePath, e.Markdown);
            string targetPath = viewModel.ResolveMentionPath(e.RelativePath);
            bool opened = await OpenNoteAsync(targetPath, selectInTree: true);
            EditorViewControl.PostMentionOpenResult(e.RelativePath, opened, opened ? null : "The mentioned note could not be opened.");
        }
        catch (Exception ex)
        {
            EditorViewControl.PostMentionOpenResult(e.RelativePath, ok: false, error: ex.Message);
        }
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
        }
    }

    private void RenameTreeItem_Click(object sender, RoutedEventArgs e)
    {
        RenameSelectedTreeNode();
    }

    private void DeleteTreeItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedTreeNode();
    }

    private void RenameSelectedTreeNode()
    {
        RenameTreeNode(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void DeleteSelectedTreeNode()
    {
        DeleteTreeNode(NotesTree.SelectedItem as NoteTreeNodeViewModel);
    }

    private void RenameTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (!viewModel.CanRenameNode(node))
        {
            MessageBox.Show(
                "This item cannot be renamed.",
                "Rename",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string itemKind = viewModel.GetNodeKind(node);
        RenameItemDialog dialog = new(itemKind, viewModel.GetEditableName(node))
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            viewModel.RenameNode(node, dialog.ItemName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {itemKind} could not be renamed:{Environment.NewLine}{ex.Message}",
                "Rename Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void DeleteTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (!viewModel.CanDeleteNode(node))
        {
            MessageBox.Show(
                "This item cannot be deleted.",
                "Delete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string itemKind = viewModel.GetNodeKind(node);
        string message = node.IsMarkdownFile
            ? $"Delete Markdown file \"{node.Header}\"?"
            : $"Delete folder \"{node.Header}\" and everything inside it?";

        MessageBoxResult result = MessageBox.Show(
            message,
            "Confirm Delete",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            viewModel.DeleteNode(node);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {itemKind} could not be deleted:{Environment.NewLine}{ex.Message}",
                "Delete Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void NotesTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || FindTreeViewItem(e.OriginalSource) is not TreeViewItem item
            || !TryGetMarkdownNodePath(item, requireFile: true, out string notePath))
        {
            return;
        }

        DragDrop.DoDragDrop(item, new DataObject(NoteTreeDragDataFormat, notePath), DragDropEffects.Move);
    }

    private void NotesTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = FindTreeViewItem(e.OriginalSource) is TreeViewItem item
            && CanDropNoteOnTarget(item, e, out _, out _)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void NotesTree_Drop(object sender, DragEventArgs e)
    {
        if (FindTreeViewItem(e.OriginalSource) is not TreeViewItem item
            || !CanDropNoteOnTarget(item, e, out string droppedNotePath, out string targetNotePath))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        try
        {
            MarkdownNoteGroupResult result = viewModel.GroupNoteWithTarget(droppedNotePath, targetNotePath);
            await OpenNoteAsync(result.TargetNotePath, selectInTree: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The notes could not be grouped:{Environment.NewLine}{ex.Message}",
                "Drag and Drop Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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

    private static bool CanDropNoteOnTarget(
        object sender,
        DragEventArgs e,
        out string droppedNotePath,
        out string targetNotePath)
    {
        droppedNotePath = string.Empty;
        targetNotePath = string.Empty;

        if (sender is not TreeViewItem targetItem
            || !TryGetMarkdownNodePath(targetItem, requireFile: true, out targetNotePath)
            || !e.Data.GetDataPresent(NoteTreeDragDataFormat)
            || e.Data.GetData(NoteTreeDragDataFormat) is not string draggedPath
            || string.IsNullOrWhiteSpace(draggedPath)
            || string.Equals(draggedPath, targetNotePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        droppedNotePath = draggedPath;
        return true;
    }

    private static bool TryGetMarkdownNodePath(
        TreeViewItem item,
        bool requireFile,
        out string notePath)
    {
        notePath = string.Empty;

        if (item.DataContext is not NoteTreeNodeViewModel node
            || string.IsNullOrWhiteSpace(node.NotePath)
            || (requireFile && !node.IsMarkdownFile))
        {
            return false;
        }

        notePath = node.NotePath;
        return true;
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

    private void SendMentionTargets()
    {
        try
        {
            EditorViewControl.PostMentionTargets(viewModel.ListMentionTargets());
        }
        catch (Exception ex)
        {
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

    private async Task RenderReviewCardAsync(string markdown)
    {
        if (reviewView is null)
        {
            throw new InvalidOperationException("Review mode is not active.");
        }

        await EditorViewControl.RenderCardInHostAsync(reviewView.CardRendererHostControl, markdown);
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

        if (ReferenceEquals(OverlayHost.Content, view))
        {
            OverlayHost.Content = null;
        }

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
        EditorViewControl.DetachBlockSuiteHost();
        EditorViewControl.Visibility = Visibility.Visible;
        EditorViewControl.IsEnabled = true;
        EditorViewControl.IsHitTestVisible = true;
        OverlayHost.Content = overlay;
        OverlayHost.Visibility = Visibility.Visible;
        OverlayHost.IsHitTestVisible = true;
    }

    private void HideOverlay()
    {
        OverlayHost.Content = null;
        OverlayHost.Visibility = Visibility.Collapsed;
        OverlayHost.IsHitTestVisible = false;
        EditorViewControl.Visibility = Visibility.Visible;
        EditorViewControl.IsEnabled = true;
        EditorViewControl.IsHitTestVisible = true;
    }
}
