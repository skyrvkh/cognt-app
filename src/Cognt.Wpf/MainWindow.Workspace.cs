using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels.Navigation;
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
    private WorkspaceToolDockingPolicy? rightPaneDockingPolicy;
    private bool isCommittingNoteTabRename;

    private WorkspaceToolDockingPolicy RightPaneDockingPolicy => rightPaneDockingPolicy ??= new(
        Dispatcher,
        RightToolPaneGroup,
        InspectorAnchorable,
        GraphAnchorable,
        CardsAnchorable,
        PdfAnchorable,
        BrowserAnchorable);

    private Task QueueOpenNoteDocumentAsync(string notePath, bool selectInTree)
    {
        return QueueOpenNoteDocumentAsync(notePath, selectInTree, isSessionRestoreRequest: false);
    }

    private Task QueueRestoreOpenNoteDocumentAsync(string notePath, bool selectInTree)
    {
        return QueueOpenNoteDocumentAsync(notePath, selectInTree, isSessionRestoreRequest: true);
    }

    private Task QueueOpenNoteDocumentAsync(string notePath, bool selectInTree, bool isSessionRestoreRequest)
    {
        if (!isSessionRestoreRequest && isRestoringWorkspaceSession)
        {
            isWorkspaceSessionRestoreInterrupted = true;
        }

        if (openingNoteDocument is { } openingSession
            && !openingSession.IsClosed
            && IsSamePath(openingSession.NotePath, notePath))
        {
            return Task.CompletedTask;
        }

        int requestVersion = ++noteOpenRequestVersion;
        CancelSupersededOpeningNoteDocument(notePath, requestVersion);
        return OpenSerializedNoteDocumentAsync(notePath, selectInTree, requestVersion);
    }

    private async Task OpenSerializedNoteDocumentAsync(string notePath, bool selectInTree, int requestVersion)
    {
        await openNoteDocumentGate.WaitAsync();

        try
        {
            if (IsSupersededNoteOpenRequest(requestVersion))
            {
                return;
            }

            await OpenOrActivateNoteDocumentAsync(notePath, selectInTree, requestVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Serialized note open failed. NotePath={NotePath}", notePath);
            if (!IsSupersededNoteOpenRequest(requestVersion) && !isRestoringWorkspaceSession)
            {
                shellDialogs.ShowWarning("Open Note Failed", $"The editor could not open this note: {ex.Message}");
            }
        }
        finally
        {
            openNoteDocumentGate.Release();
        }
    }

    private async Task OpenOrActivateNoteDocumentAsync(string notePath, bool selectInTree, int requestVersion)
    {
        if (openNoteDocuments.TryGetValue(notePath, out NoteWorkspaceDocumentSession? existingSession))
        {
            if (existingSession.IsClosed || !existingSession.HasMountedEditor)
            {
                logger.LogWarning(
                    "Discarding stale note session before reopening. NotePath={NotePath} IsClosed={IsClosed} HasAttachedEditor={HasAttachedEditor} IsHostReady={IsHostReady}",
                    notePath,
                    existingSession.IsClosed,
                    existingSession.HasAttachedEditor,
                    existingSession.IsHostReady);
                DiscardNoteDocumentSession(existingSession);
            }
            else
            {
                if (existingSession.IsDetached)
                {
                    await existingSession.RedockAsync(WorkspaceDocumentPane);
                }

                ActivateMainWorkspaceWindow();
                ActivateNoteDocument(existingSession, selectInTree);
                if (TakePendingCaretRestoreState(notePath) is { } pendingCaret)
                {
                    await existingSession.RestoreCaretStateAsync(pendingCaret);
                }

                existingSession.Focus();
                return;
            }
        }

        PreparedNote? note;
        try
        {
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
                    DiscardNoteDocumentSession(currentSession);
                    return;
                }

                ActivateNoteDocument(currentSession, selectInTree);
            }

            return;
        }

        EditorCaretState? restoreCaretState = TakePendingCaretRestoreState(note.NotePath);
        NoteWorkspaceDocumentSession session = new(
            viewModel,
            editorViewFactory,
            shellDialogs,
            imageMaskEditorDialogs,
            QueueOpenNoteDocumentAsync,
            RefreshOpenDocumentMentionTargets,
            CreateEditorTheme,
            loggerFactory.CreateLogger<NoteWorkspaceDocumentSession>(),
            note,
            restoreCaretState);
        session.Closed += NoteDocumentSession_Closed;
        session.Activated += NoteDocumentSession_Activated;
        session.CurrentOutlineBlockChanged += NoteDocumentSession_CurrentOutlineBlockChanged;

        openNoteDocuments[note.NotePath] = session;
        openingNoteDocument = session;
        session.LayoutDocument.CanFloat = false;
        WorkspaceDocumentPane.Children.Add(session.LayoutDocument);
        session.LayoutDocument.IsSelected = true;
        session.LayoutDocument.IsActive = true;
        ActivateMainWorkspaceWindow();
        WorkspaceDockingManager.UpdateLayout();

        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (IsSupersededNoteOpenRequest(requestVersion))
            {
                DiscardNoteDocumentSession(session);
                return;
            }

            await session.InitializeAsync();
            if (IsSupersededNoteOpenRequest(requestVersion))
            {
                DiscardNoteDocumentSession(session);
                return;
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Note document session initialization failed. NotePath={NotePath}", note.NotePath);
            DiscardNoteDocumentSession(session);
            if (!IsSupersededNoteOpenRequest(requestVersion) && !isRestoringWorkspaceSession)
            {
                shellDialogs.ShowWarning("Open Note Failed", $"The editor could not open this note: {ex.Message}");
            }
            else
            {
                logger.LogWarning(
                    ex,
                    "Note document session failure warning suppressed because the request was superseded or session restore is active. NotePath={NotePath}",
                    note.NotePath);
            }

            return;
        }
        finally
        {
            if (ReferenceEquals(openingNoteDocument, session))
            {
                openingNoteDocument = null;
            }
        }

        ActivateNoteDocument(session, selectInTree);
    }

    private bool IsSupersededNoteOpenRequest(int requestVersion)
    {
        return requestVersion != noteOpenRequestVersion;
    }

    private void CancelSupersededOpeningNoteDocument(string requestedNotePath, int requestVersion)
    {
        NoteWorkspaceDocumentSession? session = openingNoteDocument;
        if (session is null
            || session.IsClosed
            || IsSamePath(session.NotePath, requestedNotePath))
        {
            return;
        }

        DiscardNoteDocumentSession(session);
    }

    private void DiscardNoteDocumentSession(NoteWorkspaceDocumentSession session)
    {
        bool wasActive = ReferenceEquals(activeNoteDocument, session);

        session.Closed -= NoteDocumentSession_Closed;
        session.Activated -= NoteDocumentSession_Activated;
        session.CurrentOutlineBlockChanged -= NoteDocumentSession_CurrentOutlineBlockChanged;
        openNoteDocuments.Remove(session.NotePath);
        if (ReferenceEquals(openingNoteDocument, session))
        {
            openingNoteDocument = null;
        }

        session.Dispose();

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

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
        if (session.IsClosed || !session.IsHostReady)
        {
            return;
        }

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
            noteInspectorViewModel.UpdateCurrentContent(session.NotePath, session.GetContent());
        }

        session.Focus(focusEditor: !isRestoringWorkspaceSession);

        if (selectInTree)
        {
            noteTreeInteractions.RunWithSuppressedTreeSelection(
                () => SelectNote(session.NotePath),
                deferRestore: true);
        }

        QueueWorkspaceSessionSave();
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

        FlashcardWorkspaceDocumentSession? flashcardSession = openFlashcardDocuments.Values.FirstOrDefault(candidate =>
            candidate.HasMountedEditor
            && candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent));
        if (flashcardSession is not null)
        {
            activeNoteDocument = null;
            flashcardSession.Focus();
            return;
        }

        activeNoteDocument = null;
    }

    private void RemoveNoteDocument(NoteWorkspaceDocumentSession session)
    {
        bool wasActive = ReferenceEquals(activeNoteDocument, session);

        session.Closed -= NoteDocumentSession_Closed;
        session.Activated -= NoteDocumentSession_Activated;
        session.CurrentOutlineBlockChanged -= NoteDocumentSession_CurrentOutlineBlockChanged;
        openNoteDocuments.Remove(session.NotePath);
        session.Dispose();

        if (!wasActive)
        {
            return;
        }

        activeNoteDocument = openNoteDocuments.Values.FirstOrDefault(candidate => !candidate.IsClosed && candidate.IsHostReady);
        if (activeNoteDocument is not null)
        {
            ActivateNoteDocument(activeNoteDocument, selectInTree: false);
        }
        else
        {
            viewModel.ClearActiveWorkspaceNoteIfMatches(session.NotePath);
        }

        QueueWorkspaceSessionSave();
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

        NoteWorkspaceDocumentSession[] matchedSessions = openNoteDocuments.Values
            .Where(candidate => deletedPaths.Contains(Path.GetFullPath(candidate.NotePath)))
            .ToArray();

        foreach (NoteWorkspaceDocumentSession session in matchedSessions)
        {
            await CloseNoteDocumentAsync(session, savePendingChanges: false);
        }
    }

    private async Task CloseNoteDocumentAsync(NoteWorkspaceDocumentSession session, bool savePendingChanges = true)
    {

        await session.CloseAsync(savePendingChanges);

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

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
        if (activeNoteDocument is not null && !activeNoteDocument.IsClosed && activeNoteDocument.IsHostReady)
        {
            return activeNoteDocument;
        }

        return openNoteDocuments.Values.FirstOrDefault(candidate =>
            candidate.IsHostReady
            && (candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent)
                || candidate.LayoutDocument.IsActive
                || candidate.LayoutDocument.IsSelected));
    }

    private string? GetActiveWorkspaceEditorNotePath()
    {
        return GetActiveNoteDocument()?.NotePath;
    }

    private string GetActiveWorkspaceEditorContent()
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return string.Empty;
        }

        return session.GetContent();
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
        if (RightPaneDockingPolicy.CancelWorkspaceToolClosing(e.Anchorable))
        {
            e.Cancel = true;
        }
    }

    private void WorkspaceDockingManager_AnchorableHiding(object? sender, AnchorableHidingEventArgs e)
    {
        if (RightPaneDockingPolicy.CancelWorkspaceToolHiding(e.Anchorable))
        {
            e.Cancel = true;
            e.CloseInsteadOfHide = false;
        }
    }

    private void WorkspaceDockingManager_AnchorableHidden(object? sender, AnchorableHiddenEventArgs e)
    {
        RightPaneDockingPolicy.RestoreWorkspaceToolIfOwned(e.Anchorable);
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

        foreach (FlashcardWorkspaceDocumentSession session in openFlashcardDocuments.Values)
        {
            session.LayoutDocument.CanFloat = false;
        }
    }

    private void ConfigureRightPaneTabs()
    {
        RightPaneDockingPolicy.ConfigureTabs();
    }

    private void EnforceRightPaneTabPolicy()
    {
        RightPaneDockingPolicy.EnforceRightPaneTabs();
    }

    private void ApplyDefaultLowerRightToolAutoHide()
    {
        RightPaneDockingPolicy.ApplyDefaultLowerRightToolAutoHide();
    }

    private void EnsureToolAutoHidden(LayoutAnchorable anchorable)
    {
        RightPaneDockingPolicy.EnsureToolAutoHidden(anchorable);
    }

    private void RestoreHiddenWorkspaceTools()
    {
        RightPaneDockingPolicy.RestoreHiddenWorkspaceTools();
    }

    private void ProtectToolFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        RightPaneDockingPolicy.ProtectFloatingWindow(floatingWindow);
    }

    private void UnprotectToolFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        RightPaneDockingPolicy.UnprotectFloatingWindow(floatingWindow);
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
            logger.LogError(
                ex,
                "Note tab rename failed. NotePath={NotePath} ItemId={ItemId} NewTitle={NewTitle}",
                session.NotePath,
                itemId,
                newTitle);
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
