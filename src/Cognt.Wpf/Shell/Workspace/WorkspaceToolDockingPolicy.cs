using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace CogntApp.Shell.Workspace;

internal sealed class WorkspaceToolDockingPolicy
{
    private const double RightPaneDockWidth = 420.0d;
    private const double BrowserPaneDockWidth = 560.0d;

    private readonly Dispatcher dispatcher;
    private readonly LayoutAnchorablePaneGroup rightPaneGroup;
    private readonly LayoutAnchorablePaneGroup cardsPaneGroup;
    private readonly LayoutAnchorablePaneGroup browserPaneGroup;
    private readonly LayoutAnchorable inspectorTool;
    private readonly LayoutAnchorable graphTool;
    private readonly LayoutAnchorable cardsTool;
    private readonly LayoutAnchorable browserTool;
    private readonly LayoutAnchorable[] workspaceTools;
    private readonly Dictionary<LayoutFloatingWindowControl, CancelEventHandler> protectedToolWindowHandlers = new();
    private bool lowerRightToolsAutoHideApplied;
    private bool browserDockRequestQueued;
    private bool inspectorAutoHideRequested;
    private bool graphAutoHideRequested;

    internal WorkspaceToolDockingPolicy(
        Dispatcher dispatcher,
        LayoutAnchorablePaneGroup rightPaneGroup,
        LayoutAnchorablePaneGroup cardsPaneGroup,
        LayoutAnchorablePaneGroup browserPaneGroup,
        LayoutAnchorable inspectorTool,
        LayoutAnchorable graphTool,
        LayoutAnchorable cardsTool,
        LayoutAnchorable browserTool)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(rightPaneGroup);
        ArgumentNullException.ThrowIfNull(cardsPaneGroup);
        ArgumentNullException.ThrowIfNull(browserPaneGroup);
        ArgumentNullException.ThrowIfNull(inspectorTool);
        ArgumentNullException.ThrowIfNull(graphTool);
        ArgumentNullException.ThrowIfNull(cardsTool);
        ArgumentNullException.ThrowIfNull(browserTool);

        this.dispatcher = dispatcher;
        this.rightPaneGroup = rightPaneGroup;
        this.cardsPaneGroup = cardsPaneGroup;
        this.browserPaneGroup = browserPaneGroup;
        this.inspectorTool = inspectorTool;
        this.graphTool = graphTool;
        this.cardsTool = cardsTool;
        this.browserTool = browserTool;
        workspaceTools =
        [
            inspectorTool,
            graphTool,
            cardsTool,
            browserTool
        ];
    }

    internal void ConfigureTabs()
    {
        foreach (LayoutAnchorable anchorable in workspaceTools)
        {
            anchorable.Closing -= RightPaneTab_Closing;
            anchorable.Closing += RightPaneTab_Closing;
            anchorable.Hiding -= RightPaneTab_Hiding;
            anchorable.Hiding += RightPaneTab_Hiding;
        }

        browserTool.PropertyChanged -= BrowserTool_PropertyChanged;
        browserTool.PropertyChanged += BrowserTool_PropertyChanged;
        inspectorTool.PropertyChanged -= NoteTool_PropertyChanged;
        inspectorTool.PropertyChanged += NoteTool_PropertyChanged;
        graphTool.PropertyChanged -= NoteTool_PropertyChanged;
        graphTool.PropertyChanged += NoteTool_PropertyChanged;
        inspectorAutoHideRequested = inspectorTool.IsAutoHidden;
        graphAutoHideRequested = graphTool.IsAutoHidden;
        EnforceRightPaneTabs();
    }

    internal void EnforceRightPaneTabs()
    {
        rightPaneGroup.DockWidth = new GridLength(RightPaneDockWidth);
        rightPaneGroup.FloatingWidth = RightPaneDockWidth;
        cardsPaneGroup.DockWidth = new GridLength(RightPaneDockWidth);
        cardsPaneGroup.FloatingWidth = RightPaneDockWidth;
        browserPaneGroup.DockWidth = new GridLength(BrowserPaneDockWidth);
        browserPaneGroup.FloatingWidth = BrowserPaneDockWidth;

        foreach (LayoutAnchorable anchorable in workspaceTools)
        {
            anchorable.CanClose = false;
            anchorable.CanFloat = false;
            anchorable.CanHide = IsNoteContextTool(anchorable)
                || ReferenceEquals(anchorable, browserTool);
            anchorable.CanAutoHide = true;
            double width = ReferenceEquals(anchorable, browserTool)
                ? BrowserPaneDockWidth
                : RightPaneDockWidth;
            anchorable.AutoHideWidth = width;
            anchorable.FloatingWidth = width;
        }
    }

    internal void ApplyDefaultLowerRightToolAutoHide()
    {
        if (lowerRightToolsAutoHideApplied)
        {
            return;
        }

        lowerRightToolsAutoHideApplied = true;
        _ = dispatcher.BeginInvoke(() =>
        {
            EnforceRightPaneTabs();
            EnsureToolAutoHidden(cardsTool);
            EnsureToolAutoHidden(browserTool);

            if (!inspectorTool.IsAutoHidden)
            {
                ShowDockTool(inspectorTool);
            }

            if (!graphTool.IsAutoHidden)
            {
                graphTool.Show();
            }
        }, DispatcherPriority.Loaded);
    }

    internal void EnsureToolAutoHidden(LayoutAnchorable anchorable)
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

    internal bool CancelWorkspaceToolClosing(LayoutAnchorable anchorable)
    {
        if (!IsWorkspaceTool(anchorable))
        {
            return false;
        }

        EnforceRightPaneTabs();
        return true;
    }

    internal bool CancelWorkspaceToolHiding(LayoutAnchorable anchorable)
    {
        if (!IsWorkspaceTool(anchorable))
        {
            return false;
        }

        EnforceRightPaneTabs();
        RecordUserAutoHideRequest(anchorable, isAutoHidden: true);
        QueueToolAutoHide(anchorable);
        return true;
    }

    internal bool RestoreWorkspaceToolIfOwned(LayoutAnchorable anchorable)
    {
        if (!IsWorkspaceTool(anchorable))
        {
            return false;
        }

        RestoreWorkspaceTool(anchorable);
        return true;
    }

    internal void RestoreHiddenWorkspaceTools()
    {
        foreach (LayoutAnchorable anchorable in workspaceTools)
        {
            if (anchorable.IsHidden)
            {
                RestoreWorkspaceTool(anchorable);
            }
        }
    }

    internal void ProtectFloatingWindow(LayoutFloatingWindowControl floatingWindow)
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
            EnforceRightPaneTabs();
        };

        floatingWindow.Closing += handler;
        protectedToolWindowHandlers[floatingWindow] = handler;
    }

    internal void UnprotectFloatingWindow(LayoutFloatingWindowControl floatingWindow)
    {
        if (protectedToolWindowHandlers.Remove(floatingWindow, out CancelEventHandler? handler))
        {
            floatingWindow.Closing -= handler;
        }
    }

    private void RightPaneTab_Closing(object? sender, CancelEventArgs e)
    {
        if (sender is LayoutAnchorable anchorable && CancelWorkspaceToolClosing(anchorable))
        {
            e.Cancel = true;
        }
    }

    private void RightPaneTab_Hiding(object? sender, CancelEventArgs e)
    {
        if (sender is LayoutAnchorable anchorable && CancelWorkspaceToolHiding(anchorable))
        {
            e.Cancel = true;
        }
    }

    private void RestoreWorkspaceTool(LayoutAnchorable anchorable)
    {
        _ = dispatcher.BeginInvoke(() =>
        {
            if (!IsWorkspaceTool(anchorable))
            {
                return;
            }

            EnforceRightPaneTabs();
            EnsureToolAutoHidden(anchorable);
        }, DispatcherPriority.Background);
    }

    private void QueueToolAutoHide(LayoutAnchorable anchorable)
    {
        _ = dispatcher.BeginInvoke(() =>
        {
            if (IsWorkspaceTool(anchorable))
            {
                EnsureToolAutoHidden(anchorable);
            }
        }, DispatcherPriority.Background);
    }

    private void BrowserTool_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LayoutAnchorable.IsActive)
            || !browserTool.IsActive
            || !browserTool.IsAutoHidden
            || browserDockRequestQueued)
        {
            return;
        }

        bool restoreInspector = !inspectorAutoHideRequested;
        bool restoreGraph = !graphAutoHideRequested;
        browserDockRequestQueued = true;
        _ = dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (browserTool.IsActive && browserTool.IsAutoHidden)
                {
                    browserTool.ToggleAutoHide();
                    browserPaneGroup.DockWidth = new GridLength(BrowserPaneDockWidth);
                    browserTool.IsSelected = true;
                    QueueRestoreDockedNoteTools(restoreInspector, restoreGraph);
                }
            }
            finally
            {
                browserDockRequestQueued = false;
            }
        }, DispatcherPriority.Background);
    }

    private void NoteTool_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LayoutAnchorable.IsAutoHidden)
            || browserDockRequestQueued
            || sender is not LayoutAnchorable anchorable)
        {
            return;
        }

        RecordUserAutoHideRequest(anchorable, anchorable.IsAutoHidden);
    }

    private void RecordUserAutoHideRequest(LayoutAnchorable anchorable, bool isAutoHidden)
    {
        if (ReferenceEquals(anchorable, inspectorTool))
        {
            inspectorAutoHideRequested = isAutoHidden;
        }
        else if (ReferenceEquals(anchorable, graphTool))
        {
            graphAutoHideRequested = isAutoHidden;
        }
    }

    private void QueueRestoreDockedNoteTools(bool restoreInspector, bool restoreGraph)
    {
        _ = dispatcher.BeginInvoke(() =>
        {
            RestoreDockedTool(inspectorTool, restoreInspector);
            RestoreDockedTool(graphTool, restoreGraph);
            browserTool.IsSelected = true;
            browserTool.IsActive = true;
        }, DispatcherPriority.ContextIdle);
    }

    private static void RestoreDockedTool(LayoutAnchorable anchorable, bool shouldRestore)
    {
        if (!shouldRestore)
        {
            return;
        }

        if (anchorable.IsHidden)
        {
            anchorable.Show();
        }

        if (anchorable.IsAutoHidden)
        {
            anchorable.ToggleAutoHide();
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

    private bool IsWorkspaceTool(LayoutAnchorable anchorable)
    {
        return ReferenceEquals(anchorable, inspectorTool)
            || ReferenceEquals(anchorable, graphTool)
            || ReferenceEquals(anchorable, cardsTool)
            || ReferenceEquals(anchorable, browserTool);
    }

    private bool IsNoteContextTool(LayoutAnchorable anchorable)
    {
        return ReferenceEquals(anchorable, inspectorTool)
            || ReferenceEquals(anchorable, graphTool);
    }

    private static void ShowDockTool(LayoutAnchorable anchorable)
    {
        anchorable.Show();
        anchorable.IsSelected = true;
        anchorable.IsActive = true;
    }
}
