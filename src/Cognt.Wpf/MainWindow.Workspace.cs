using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels.Navigation;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CogntApp;

public partial class MainWindow
{
    private const double RightPaneDockWidth = 420.0d;
    private readonly Dictionary<LayoutFloatingWindowControl, CancelEventHandler> protectedToolWindowHandlers = new();

    private async Task OpenOrActivateNoteDocumentAsync(string notePath, bool selectInTree)
    {
        if (openNoteDocuments.TryGetValue(notePath, out NoteWorkspaceDocumentSession? existingSession))
        {
            if (existingSession.IsClosed)
            {
                RemoveNoteDocument(existingSession);
            }
            else
            {
                if (existingSession.IsDetached)
                {
                    await existingSession.RedockAsync(WorkspaceDocumentPane);
                }

                ActivateMainWorkspaceWindow();
                ActivateNoteDocument(existingSession, selectInTree);
                existingSession.Focus();
                return;
            }
        }

        PreparedNote? note;
        try
        {
            note = viewModel.PrepareNoteForOpen(notePath);
        }
        catch (Exception ex)
        {
            shellDialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
            return;
        }

        if (note is null)
        {
            if (openNoteDocuments.TryGetValue(notePath, out NoteWorkspaceDocumentSession? currentSession))
            {
                ActivateNoteDocument(currentSession, selectInTree);
            }

            return;
        }

        NoteWorkspaceDocumentSession session = new(
            viewModel,
            editorViewFactory,
            shellDialogs,
            imageMaskEditorDialogs,
            OpenOrActivateNoteDocumentAsync,
            RefreshOpenDocumentMentionTargets,
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
            await session.InitializeAsync();
        }
        catch (Exception ex)
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
            openNoteDocuments.Remove(note.NotePath);
            session.Dispose();
            shellDialogs.ShowWarning("Open Note Failed", $"The editor could not open this note: {ex.Message}");
            return;
        }

        ActivateNoteDocument(session, selectInTree);
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
        activeNoteDocument = session;
        if (session.IsDocked)
        {
            session.LayoutDocument.IsSelected = true;
            session.LayoutDocument.IsActive = true;
        }

        session.Activate(selectInTree);
        noteInspectorViewModel.UpdateCurrentContent(session.GetContent());

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
            candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent));
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

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

        await session.CloseAsync();
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
            candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent)
            || candidate.LayoutDocument.IsActive
            || candidate.LayoutDocument.IsSelected);
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
        if (ResolveHeaderDocument(sender as DependencyObject) is not { } document)
        {
            return;
        }

        document.IsSelected = true;
        document.IsActive = true;
        noteDocumentDragController.Begin(document, ToScreenDip(((FrameworkElement)sender).PointToScreen(e.GetPosition((IInputElement)sender))));
        Mouse.Capture(sender as IInputElement);
        e.Handled = true;
    }

    private async void NoteDocumentHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
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

    private void NoteDocumentHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        noteDocumentDragController.Reset();
    }

    private async Task DetachNoteDocumentAsync(LayoutDocument document, Point screenPoint)
    {
        NoteWorkspaceDocumentSession? session = openNoteDocuments.Values.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.LayoutDocument, document));
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
