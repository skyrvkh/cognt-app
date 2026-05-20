using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels.Navigation;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private const double RightPaneDockWidth = 420.0d;
    private readonly Dictionary<LayoutFloatingWindowControl, CancelEventHandler> protectedToolWindowHandlers = new();
    private bool isCommittingNoteTabRename;

    private Task QueueOpenNoteDocumentAsync(string notePath, bool selectInTree)
    {
        logger.LogDebug(
            "Queueing note open. NotePath={NotePath} SelectInTree={SelectInTree}",
            notePath,
            selectInTree);
        return OpenSerializedNoteDocumentAsync(notePath, selectInTree);
    }

    private async Task OpenSerializedNoteDocumentAsync(string notePath, bool selectInTree)
    {
        logger.LogDebug("Waiting for note open gate. NotePath={NotePath}", notePath);
        await openNoteDocumentGate.WaitAsync();

        try
        {
            logger.LogDebug("Entered note open gate. NotePath={NotePath}", notePath);
            await OpenOrActivateNoteDocumentAsync(notePath, selectInTree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Serialized note open failed. NotePath={NotePath}", notePath);
            shellDialogs.ShowWarning("Open Note Failed", $"The editor could not open this note: {ex.Message}");
        }
        finally
        {
            logger.LogDebug("Leaving note open gate. NotePath={NotePath}", notePath);
            openNoteDocumentGate.Release();
        }
    }

    private async Task OpenOrActivateNoteDocumentAsync(string notePath, bool selectInTree)
    {
        if (openNoteDocuments.TryGetValue(notePath, out NoteWorkspaceDocumentSession? existingSession))
        {
            logger.LogDebug(
                "Found existing note session. NotePath={NotePath} IsClosed={IsClosed} HasMountedEditor={HasMountedEditor} IsDetached={IsDetached}",
                notePath,
                existingSession.IsClosed,
                existingSession.HasMountedEditor,
                existingSession.IsDetached);
            if (existingSession.IsClosed || !existingSession.HasMountedEditor)
            {
                logger.LogWarning(
                    "Discarding stale note session before reopening. NotePath={NotePath} IsClosed={IsClosed} HasMountedEditor={HasMountedEditor}",
                    notePath,
                    existingSession.IsClosed,
                    existingSession.HasMountedEditor);
                DiscardNoteDocumentSession(existingSession);
            }
            else
            {
                if (existingSession.IsDetached)
                {
                    logger.LogInformation("Redocking existing note session. NotePath={NotePath}", notePath);
                    await existingSession.RedockAsync(WorkspaceDocumentPane);
                }

                logger.LogInformation("Activating existing note session. NotePath={NotePath}", notePath);
                ActivateMainWorkspaceWindow();
                ActivateNoteDocument(existingSession, selectInTree);
                existingSession.Focus();
                return;
            }
        }

        PreparedNote? note;
        try
        {
            logger.LogDebug("Preparing note for open. NotePath={NotePath}", notePath);
            note = viewModel.PrepareNoteForOpen(notePath, forceRead: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Prepare note for open failed. NotePath={NotePath}", notePath);
            shellDialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
            return;
        }

        if (note is null)
        {
            logger.LogWarning("PrepareNoteForOpen returned null. NotePath={NotePath}", notePath);
            if (openNoteDocuments.TryGetValue(notePath, out NoteWorkspaceDocumentSession? currentSession))
            {
                if (currentSession.IsClosed || !currentSession.HasMountedEditor)
                {
                    logger.LogWarning("Discarding null-prepare stale note session. NotePath={NotePath}", notePath);
                    DiscardNoteDocumentSession(currentSession);
                    return;
                }

                logger.LogInformation("Activating current note session after null prepare. NotePath={NotePath}", notePath);
                ActivateNoteDocument(currentSession, selectInTree);
            }

            return;
        }

        logger.LogInformation(
            "Creating note document session. NotePath={NotePath} Title={Title} ContentLength={ContentLength}",
            note.NotePath,
            note.Title,
            note.Content.Length);
        NoteWorkspaceDocumentSession session = new(
            viewModel,
            editorViewFactory,
            shellDialogs,
            imageMaskEditorDialogs,
            QueueOpenNoteDocumentAsync,
            RefreshOpenDocumentMentionTargets,
            CreateEditorTheme,
            loggerFactory.CreateLogger<NoteWorkspaceDocumentSession>(),
            note);
        session.Closed += NoteDocumentSession_Closed;
        session.Activated += NoteDocumentSession_Activated;

        openNoteDocuments[note.NotePath] = session;
        session.LayoutDocument.CanFloat = false;
        WorkspaceDocumentPane.Children.Add(session.LayoutDocument);
        session.LayoutDocument.IsSelected = true;
        session.LayoutDocument.IsActive = true;
        ActivateMainWorkspaceWindow();
        WorkspaceDockingManager.UpdateLayout();

        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            logger.LogDebug("Initializing note document session. NotePath={NotePath}", note.NotePath);
            await session.InitializeAsync();
            logger.LogInformation("Note document session initialized. NotePath={NotePath}", note.NotePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Note document session initialization failed. NotePath={NotePath}", note.NotePath);
            DiscardNoteDocumentSession(session);
            shellDialogs.ShowWarning("Open Note Failed", $"The editor could not open this note: {ex.Message}");
            return;
        }

        ActivateNoteDocument(session, selectInTree);
    }

    private void DiscardNoteDocumentSession(NoteWorkspaceDocumentSession session)
    {
        logger.LogWarning(
            "Discarding note document session. NotePath={NotePath} IsClosed={IsClosed} HasMountedEditor={HasMountedEditor}",
            session.NotePath,
            session.IsClosed,
            session.HasMountedEditor);
        bool wasActive = ReferenceEquals(activeNoteDocument, session);

        session.Closed -= NoteDocumentSession_Closed;
        session.Activated -= NoteDocumentSession_Activated;
        openNoteDocuments.Remove(session.NotePath);

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

        session.Dispose();

        if (wasActive)
        {
            activeNoteDocument = null;
        }
    }

    private void ActivateMainWorkspaceWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        WorkspaceDockingManager.Focus();
    }

    private void ActivateNoteDocument(NoteWorkspaceDocumentSession session, bool selectInTree)
    {
        logger.LogDebug(
            "Activating note document. NotePath={NotePath} HasMountedEditor={HasMountedEditor} SelectInTree={SelectInTree}",
            session.NotePath,
            session.HasMountedEditor,
            selectInTree);
        bool wasActiveDocument = ReferenceEquals(activeNoteDocument, session);
        activeNoteDocument = session;
        if (session.IsDocked)
        {
            session.LayoutDocument.IsSelected = true;
            session.LayoutDocument.IsActive = true;
        }

        bool activeNoteChanged = session.Activate(selectInTree);
        if (activeNoteChanged || !wasActiveDocument)
        {
            noteInspectorViewModel.UpdateCurrentContent(session.GetContent());
        }

        if (selectInTree)
        {
            noteTreeInteractions.RunWithSuppressedTreeSelection(
                () => SelectNote(session.NotePath),
                deferRestore: true);
        }
    }

    private void WorkspaceDockingManager_ActiveContentChanged(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = openNoteDocuments.Values.FirstOrDefault(candidate =>
            candidate.HasMountedEditor
            && candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent));
        if (session is not null)
        {
            ActivateNoteDocument(session, selectInTree: true);
            return;
        }

        activeNoteDocument = null;
    }

    private void RemoveNoteDocument(NoteWorkspaceDocumentSession session)
    {
        bool wasActive = ReferenceEquals(activeNoteDocument, session);

        session.Closed -= NoteDocumentSession_Closed;
        session.Activated -= NoteDocumentSession_Activated;
        openNoteDocuments.Remove(session.NotePath);
        session.Dispose();

        if (!wasActive)
        {
            return;
        }

        activeNoteDocument = openNoteDocuments.Values.FirstOrDefault(candidate => !candidate.IsClosed);
        if (activeNoteDocument is not null)
        {
            ActivateNoteDocument(activeNoteDocument, selectInTree: false);
        }
    }

    private async Task CloseActiveNoteDocumentAsync()
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        await CloseNoteDocumentAsync(session);
    }

    private async Task CloseDeletedNoteDocumentsAsync(IReadOnlyList<string> notePaths)
    {
        if (notePaths.Count == 0)
        {
            return;
        }

        HashSet<string> deletedPaths = notePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (deletedPaths.Count == 0)
        {
            return;
        }

        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values
            .Where(candidate => deletedPaths.Contains(Path.GetFullPath(candidate.NotePath)))
            .ToArray())
        {
            await CloseNoteDocumentAsync(session, savePendingChanges: false);
        }
    }

    private async Task CloseNoteDocumentAsync(NoteWorkspaceDocumentSession session, bool savePendingChanges = true)
    {
        if (savePendingChanges)
        {
            await session.SavePendingChangesAsync();
        }

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

        await session.CloseAsync(savePendingChanges: false);
    }

    private void RefreshOpenDocumentMentionTargets()
    {
        _ = Task.Run(async () =>
        {
            foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values.ToArray())
            {
                if (session.IsClosed)
                {
                    Dispatcher.Invoke(() => RemoveNoteDocument(session));
                    continue;
                }

                try
                {
                    await Dispatcher.InvokeAsync(async () => await session.RefreshHostStateAsync()).Task.Unwrap();
                }
                catch
                {
                    // ignore per-document refresh failures and keep the workspace usable
                }
            }
        });
    }

    private NoteWorkspaceDocumentSession? GetActiveNoteDocument()
    {
        if (activeNoteDocument is not null && !activeNoteDocument.IsClosed)
        {
            return activeNoteDocument;
        }

        return openNoteDocuments.Values.FirstOrDefault(candidate =>
            candidate.HasMountedEditor
            && (candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent)
                || candidate.LayoutDocument.IsActive
                || candidate.LayoutDocument.IsSelected));
    }

    private static void ShowDockTool(LayoutAnchorable anchorable)
    {
        anchorable.Show();
        anchorable.IsSelected = true;
        anchorable.IsActive = true;
    }

    private void NoteDocumentSession_Closed(object? sender, EventArgs e)
    {
        if (sender is NoteWorkspaceDocumentSession session)
        {
            RemoveNoteDocument(session);
        }
    }

    private void NoteDocumentSession_Activated(object? sender, EventArgs e)
    {
        if (sender is NoteWorkspaceDocumentSession session && !session.IsClosed)
        {
            ActivateNoteDocument(session, selectInTree: true);
        }
    }

    private void RegisterWorkspaceDockingEvents()
    {
        ConfigureRightPaneTabs();
        WorkspaceDockingManager.DocumentClosing += WorkspaceDockingManager_DocumentClosing;
        WorkspaceDockingManager.DocumentClosed += WorkspaceDockingManager_DocumentClosed;
        WorkspaceDockingManager.AnchorableClosing += WorkspaceDockingManager_AnchorableClosing;
        WorkspaceDockingManager.AnchorableHiding += WorkspaceDockingManager_AnchorableHiding;
        WorkspaceDockingManager.AnchorableHidden += WorkspaceDockingManager_AnchorableHidden;
        WorkspaceDockingManager.LayoutChanging += WorkspaceDockingManager_LayoutChanging;
        WorkspaceDockingManager.LayoutChanged += WorkspaceDockingManager_LayoutChanged;
        WorkspaceDockingManager.LayoutFloatingWindowControlCreated += WorkspaceDockingManager_LayoutFloatingWindowControlCreated;
        WorkspaceDockingManager.LayoutFloatingWindowControlClosed += WorkspaceDockingManager_LayoutFloatingWindowControlClosed;
    }

    private void WorkspaceDockingManager_DocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
    }

    private void WorkspaceDockingManager_DocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
    }

    private void WorkspaceDockingManager_AnchorableClosing(object? sender, AnchorableClosingEventArgs e)
    {
        if (IsWorkspaceTool(e.Anchorable))
        {
            e.Cancel = true;
            EnforceRightPaneTabPolicy();
        }
    }

    private void WorkspaceDockingManager_AnchorableHiding(object? sender, AnchorableHidingEventArgs e)
    {
        if (IsWorkspaceTool(e.Anchorable))
        {
            e.Cancel = true;
            e.CloseInsteadOfHide = false;
            EnforceRightPaneTabPolicy();
        }
    }

    private void WorkspaceDockingManager_AnchorableHidden(object? sender, AnchorableHiddenEventArgs e)
    {
        if (IsWorkspaceTool(e.Anchorable))
        {
            RestoreWorkspaceTool(e.Anchorable);
        }
    }

    private void WorkspaceDockingManager_LayoutChanging(object? sender, EventArgs e)
    {
        EnforceNoteDocumentDockingPolicy();
        EnforceRightPaneTabPolicy();
    }

    private void WorkspaceDockingManager_LayoutChanged(object? sender, EventArgs e)
    {
        EnforceNoteDocumentDockingPolicy();
        EnforceRightPaneTabPolicy();
        RestoreHiddenWorkspaceTools();
        RefreshOpenDocumentMentionTargets();
    }

    private void WorkspaceDockingManager_LayoutFloatingWindowControlCreated(object? sender, LayoutFloatingWindowControlCreatedEventArgs e)
    {
        EnforceNoteDocumentDockingPolicy();
        ProtectToolFloatingWindow(e.LayoutFloatingWindowControl);
        RefreshOpenDocumentMentionTargets();
    }

    private void WorkspaceDockingManager_LayoutFloatingWindowControlClosed(object? sender, LayoutFloatingWindowControlClosedEventArgs e)
    {
        UnprotectToolFloatingWindow(e.LayoutFloatingWindowControl);
        EnforceNoteDocumentDockingPolicy();
        RefreshOpenDocumentMentionTargets();
    }

    private void EnforceNoteDocumentDockingPolicy()
    {
        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values)
        {
            session.LayoutDocument.CanFloat = false;
        }
    }

    private void ConfigureRightPaneTabs()
    {
        foreach (LayoutAnchorable anchorable in EnumerateRightPaneTabs())
        {
            anchorable.Closing -= RightPaneTab_Closing;
            anchorable.Closing += RightPaneTab_Closing;
            anchorable.Hiding -= RightPaneTab_Hiding;
            anchorable.Hiding += RightPaneTab_Hiding;
        }

        EnforceRightPaneTabPolicy();
    }

    private void EnforceRightPaneTabPolicy()
    {
        RightToolPaneGroup.DockWidth = new GridLength(RightPaneDockWidth);
        RightToolPaneGroup.FloatingWidth = RightPaneDockWidth;

        foreach (LayoutAnchorable anchorable in EnumerateRightPaneTabs())
        {
            anchorable.CanClose = false;
            anchorable.CanHide = false;
            anchorable.CanAutoHide = true;
            anchorable.AutoHideWidth = RightPaneDockWidth;
            anchorable.FloatingWidth = RightPaneDockWidth;
        }
    }

    private void ApplyDefaultLowerRightToolAutoHide()
    {
        if (lowerRightToolsAutoHideApplied)
        {
            return;
        }

        lowerRightToolsAutoHideApplied = true;
        _ = Dispatcher.BeginInvoke(() =>
        {
            EnforceRightPaneTabPolicy();
            EnsureToolAutoHidden(CardsAnchorable);
            EnsureToolAutoHidden(PdfAnchorable);
            EnsureToolAutoHidden(BrowserAnchorable);

            if (!InspectorAnchorable.IsAutoHidden)
            {
                InspectorAnchorable.Show();
                InspectorAnchorable.IsSelected = true;
            }

            if (!GraphAnchorable.IsAutoHidden)
            {
                GraphAnchorable.Show();
            }
        }, DispatcherPriority.Loaded);
    }

    private static void EnsureToolAutoHidden(LayoutAnchorable anchorable)
    {
        if (anchorable.IsHidden)
        {
            anchorable.Show();
        }

        if (anchorable.CanAutoHide && !anchorable.IsAutoHidden)
        {
            anchorable.ToggleAutoHide();
        }
    }

    private void RightPaneTab_Closing(object? sender, CancelEventArgs e)
    {
        if (sender is LayoutAnchorable anchorable && IsWorkspaceTool(anchorable))
        {
            e.Cancel = true;
            EnforceRightPaneTabPolicy();
        }
    }

    private void RightPaneTab_Hiding(object? sender, CancelEventArgs e)
    {
        if (sender is LayoutAnchorable anchorable && IsWorkspaceTool(anchorable))
        {
            e.Cancel = true;
            EnforceRightPaneTabPolicy();
        }
    }

    private void RestoreHiddenWorkspaceTools()
    {
        foreach (LayoutAnchorable anchorable in EnumerateRightPaneTabs())
        {
            if (anchorable.IsHidden)
            {
                RestoreWorkspaceTool(anchorable);
            }
        }
    }

    private void RestoreWorkspaceTool(LayoutAnchorable anchorable)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsWorkspaceTool(anchorable))
            {
                return;
            }

            EnforceRightPaneTabPolicy();
            ShowDockTool(anchorable);
        }, DispatcherPriority.Background);
    }

    private void ProtectToolFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        if (protectedToolWindowHandlers.ContainsKey(floatingWindow)
            || !LayoutContainsWorkspaceTool(floatingWindow.Model))
        {
            return;
        }

        CancelEventHandler handler = (_, args) =>
        {
            if (!LayoutContainsWorkspaceTool(floatingWindow.Model))
            {
                return;
            }

            args.Cancel = true;
            EnforceRightPaneTabPolicy();
        };

        floatingWindow.Closing += handler;
        protectedToolWindowHandlers[floatingWindow] = handler;
    }

    private void UnprotectToolFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        if (protectedToolWindowHandlers.Remove(floatingWindow, out CancelEventHandler? handler))
        {
            floatingWindow.Closing -= handler;
        }
    }

    private bool LayoutContainsWorkspaceTool(object? layoutElement)
    {
        return layoutElement switch
        {
            LayoutAnchorable anchorable => IsWorkspaceTool(anchorable),
            LayoutAnchorableFloatingWindow floatingWindow => AnyLayoutChildContainsWorkspaceTool(floatingWindow.Children),
            LayoutAnchorablePaneGroup paneGroup => AnyLayoutChildContainsWorkspaceTool(paneGroup.Children),
            LayoutAnchorablePane pane => AnyLayoutChildContainsWorkspaceTool(pane.Children),
            LayoutPanel panel => AnyLayoutChildContainsWorkspaceTool(panel.Children),
            _ => false
        };
    }

    private bool AnyLayoutChildContainsWorkspaceTool(IEnumerable children)
    {
        foreach (object? child in children)
        {
            if (LayoutContainsWorkspaceTool(child))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<LayoutAnchorable> EnumerateRightPaneTabs()
    {
        yield return InspectorAnchorable;
        yield return GraphAnchorable;
        yield return CardsAnchorable;
        yield return PdfAnchorable;
        yield return BrowserAnchorable;
    }

    private bool IsWorkspaceTool(LayoutAnchorable anchorable)
    {
        return ReferenceEquals(anchorable, InspectorAnchorable)
            || ReferenceEquals(anchorable, GraphAnchorable)
            || ReferenceEquals(anchorable, CardsAnchorable)
            || ReferenceEquals(anchorable, PdfAnchorable)
            || ReferenceEquals(anchorable, BrowserAnchorable);
    }

    private void NoteDocumentHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsNoteTabRenameInput(e.OriginalSource))
        {
            return;
        }

        if (ResolveHeaderDocument(sender as DependencyObject) is not { } document)
        {
            return;
        }

        document.IsSelected = true;
        document.IsActive = true;

        if (e.ClickCount >= 2)
        {
            BeginRenameNoteTab(document, (FrameworkElement)sender);
            e.Handled = true;
            return;
        }

        noteDocumentDragController.Begin(document, ToScreenDip(((FrameworkElement)sender).PointToScreen(e.GetPosition((IInputElement)sender))));
        Mouse.Capture(sender as IInputElement);
        e.Handled = true;
    }

    private async void NoteDocumentHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (IsNoteTabRenameInput(e.OriginalSource))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed
            || ResolveHeaderDocument(sender as DependencyObject) is not { } document)
        {
            return;
        }

        Point screenPoint = ToScreenDip(((FrameworkElement)sender).PointToScreen(e.GetPosition((IInputElement)sender)));
        if (!noteDocumentDragController.ShouldDetach(document, screenPoint, GetWorkspaceScreenBounds()))
        {
            return;
        }

        Mouse.Capture(null);
        noteDocumentDragController.Reset();
        e.Handled = true;
        await DetachNoteDocumentAsync(document, screenPoint);
    }

    private async void OpenNoteInNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryResolveLayoutDocument((sender as FrameworkElement)?.DataContext, out LayoutDocument? document)
            && document is not null)
        {
            await DetachNoteDocumentAsync(document, GetDefaultDetachedWindowPoint());
        }
    }

    private void RenameNoteTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryResolveLayoutDocument((sender as FrameworkElement)?.DataContext, out LayoutDocument? document)
            && document is not null
            && GetContextMenuPlacementTarget(sender) is { } header)
        {
            BeginRenameNoteTab(document, header);
            e.Handled = true;
        }
    }

    private void NoteDocumentHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        noteDocumentDragController.Reset();
    }

    private void BeginRenameNoteTab(LayoutDocument document, FrameworkElement header)
    {
        NoteWorkspaceDocumentSession? session = FindNoteDocumentSession(document);
        if (session?.ItemId is not long)
        {
            return;
        }

        TextBox? renameTextBox = FindNamedDescendant<TextBox>(header, "DocumentDockTabRenameTextBox");
        TextBlock? titleTextBlock = FindNamedDescendant<TextBlock>(header, "DocumentDockTabTitle");
        if (renameTextBox is null)
        {
            return;
        }

        Mouse.Capture(null);
        noteDocumentDragController.Reset();
        renameTextBox.Tag = document;
        renameTextBox.Text = session.Title;
        if (titleTextBlock is not null)
        {
            titleTextBlock.Visibility = Visibility.Collapsed;
        }

        renameTextBox.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            renameTextBox.Focus();
            Keyboard.Focus(renameTextBox);
            renameTextBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void NoteDocumentTabRenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox renameTextBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitNoteTabRename(renameTextBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelNoteTabRename(renameTextBox);
            e.Handled = true;
        }
    }

    private void NoteDocumentTabRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox renameTextBox)
        {
            CommitNoteTabRename(renameTextBox);
        }
    }

    private void CommitNoteTabRename(TextBox renameTextBox)
    {
        if (isCommittingNoteTabRename || renameTextBox.Visibility != Visibility.Visible)
        {
            return;
        }

        LayoutDocument? document = renameTextBox.Tag as LayoutDocument
            ?? ResolveHeaderDocument(renameTextBox);
        NoteWorkspaceDocumentSession? session = document is null ? null : FindNoteDocumentSession(document);
        if (session?.ItemId is not long itemId)
        {
            CancelNoteTabRename(renameTextBox);
            return;
        }

        string newTitle = renameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            ShowNoteTabRenameWarning(renameTextBox, "Name is required.");
            return;
        }

        if (string.Equals(newTitle, session.Title, StringComparison.Ordinal))
        {
            CancelNoteTabRename(renameTextBox);
            return;
        }

        isCommittingNoteTabRename = true;
        try
        {
            viewModel.RenameNoteItem(itemId, newTitle);
            session.UpdateTitle(newTitle);
            EndNoteTabRename(renameTextBox);
        }
        catch (Exception ex)
        {
            shellDialogs.ShowWarning("Rename Failed", $"The note could not be renamed:{Environment.NewLine}{ex.Message}");
            RefocusNoteTabRename(renameTextBox);
        }
        finally
        {
            isCommittingNoteTabRename = false;
        }
    }

    private void ShowNoteTabRenameWarning(TextBox renameTextBox, string message)
    {
        isCommittingNoteTabRename = true;
        try
        {
            shellDialogs.ShowWarning("Rename", message);
        }
        finally
        {
            isCommittingNoteTabRename = false;
        }

        RefocusNoteTabRename(renameTextBox);
    }

    private void RefocusNoteTabRename(TextBox renameTextBox)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (renameTextBox.Visibility != Visibility.Visible)
            {
                return;
            }

            renameTextBox.Focus();
            Keyboard.Focus(renameTextBox);
            renameTextBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void CancelNoteTabRename(TextBox renameTextBox)
    {
        EndNoteTabRename(renameTextBox);
    }

    private static void EndNoteTabRename(TextBox renameTextBox)
    {
        renameTextBox.Visibility = Visibility.Collapsed;
        renameTextBox.Tag = null;

        if (VisualTreeHelper.GetParent(renameTextBox) is DependencyObject parent
            && FindNamedDescendant<TextBlock>(parent, "DocumentDockTabTitle") is { } titleTextBlock)
        {
            titleTextBlock.Visibility = Visibility.Visible;
        }
    }

    private async Task DetachNoteDocumentAsync(LayoutDocument document, Point screenPoint)
    {
        NoteWorkspaceDocumentSession? session = FindNoteDocumentSession(document);
        if (session is null || !session.IsDocked)
        {
            return;
        }

        await session.DetachToWindowAsync(
            this,
            WorkspaceDocumentPane,
            screenPoint,
            RedockNoteDocumentAsync,
            WorkspaceIntersects);
        ActivateNoteDocument(session, selectInTree: true);
    }

    private NoteWorkspaceDocumentSession? FindNoteDocumentSession(LayoutDocument document)
    {
        return openNoteDocuments.Values.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.LayoutDocument, document));
    }

    private static FrameworkElement? GetContextMenuPlacementTarget(object sender)
    {
        return ((sender as FrameworkElement)?.Parent as ContextMenu)?.PlacementTarget as FrameworkElement;
    }

    private static bool IsNoteTabRenameInput(object? source)
    {
        for (DependencyObject? current = source as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TextBox { Name: "DocumentDockTabRenameTextBox" })
            {
                return true;
            }
        }

        return false;
    }

    private static T? FindNamedDescendant<T>(DependencyObject source, string name)
        where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(source);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(source, index);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            T? descendant = FindNamedDescendant<T>(child, name);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private async Task RedockNoteDocumentAsync(NoteWorkspaceDocumentSession session)
    {
        await session.RedockAsync(WorkspaceDocumentPane);
        ActivateNoteDocument(session, selectInTree: true);
    }

    private bool WorkspaceIntersects(Rect screenBounds)
    {
        return GetWorkspaceScreenBounds().IntersectsWith(screenBounds);
    }

    private Rect GetWorkspaceScreenBounds()
    {
        Point topLeft = ToScreenDip(WorkspaceDockingManager.PointToScreen(new Point(0, 0)));
        Point bottomRight = ToScreenDip(WorkspaceDockingManager.PointToScreen(new Point(
            WorkspaceDockingManager.ActualWidth,
            WorkspaceDockingManager.ActualHeight)));
        return new Rect(topLeft, bottomRight);
    }

    private Point GetDefaultDetachedWindowPoint()
    {
        Rect workspaceBounds = GetWorkspaceScreenBounds();
        return new Point(
            workspaceBounds.Left + Math.Min(96, Math.Max(0, workspaceBounds.Width / 5)),
            workspaceBounds.Top + 42);
    }

    private Point ToScreenDip(Point screenPoint)
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice.Transform(screenPoint) ?? screenPoint;
    }

    private static LayoutDocument? ResolveHeaderDocument(DependencyObject? source)
    {
        if (source is null)
        {
            return null;
        }

        object? dataContext = (source as FrameworkElement)?.DataContext;
        if (TryResolveLayoutDocument(dataContext, out LayoutDocument? document))
        {
            return document;
        }

        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is LayoutDocumentTabItem tabItem
                && tabItem.Model is LayoutDocument tabDocument)
            {
                return tabDocument;
            }
        }

        return null;
    }

    private static bool TryResolveLayoutDocument(object? candidate, out LayoutDocument? document)
    {
        document = candidate as LayoutDocument;
        if (document is not null)
        {
            return true;
        }

        if (candidate is LayoutItem layoutItem)
        {
            document = layoutItem.Model as LayoutDocument
                ?? layoutItem.LayoutElement as LayoutDocument;
            return document is not null;
        }

        return false;
    }
}
