using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace CogntApp.Shell.Workspace;

internal sealed class WorkspaceToolDockingPolicy
{
    private const double RightPaneDockWidth = 720.0d;
    private const double InsightPaneDockWidth = 300.0d;
    private const double SidePaneDockWidth = 420.0d;

    private readonly Dispatcher dispatcher;
    private readonly LayoutAnchorablePaneGroup rightPaneGroup;
    private readonly LayoutAnchorable inspectorTool;
    private readonly LayoutAnchorable graphTool;
    private readonly LayoutAnchorable cardsTool;
    private readonly LayoutAnchorable pdfTool;
    private readonly LayoutAnchorable browserTool;
    private readonly LayoutAnchorable[] workspaceTools;
    private readonly Dictionary<LayoutFloatingWindowControl, CancelEventHandler> protectedToolWindowHandlers = new();
    private bool lowerRightToolsAutoHideApplied;

    internal WorkspaceToolDockingPolicy(
        Dispatcher dispatcher,
        LayoutAnchorablePaneGroup rightPaneGroup,
        LayoutAnchorable inspectorTool,
        LayoutAnchorable graphTool,
        LayoutAnchorable cardsTool,
        LayoutAnchorable pdfTool,
        LayoutAnchorable browserTool)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(rightPaneGroup);
        ArgumentNullException.ThrowIfNull(inspectorTool);
        ArgumentNullException.ThrowIfNull(graphTool);
        ArgumentNullException.ThrowIfNull(cardsTool);
        ArgumentNullException.ThrowIfNull(pdfTool);
        ArgumentNullException.ThrowIfNull(browserTool);

        this.dispatcher = dispatcher;
        this.rightPaneGroup = rightPaneGroup;
        this.inspectorTool = inspectorTool;
        this.graphTool = graphTool;
        this.cardsTool = cardsTool;
        this.pdfTool = pdfTool;
        this.browserTool = browserTool;
        workspaceTools =
        [
            inspectorTool,
            graphTool,
            cardsTool,
            pdfTool,
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

        EnforceRightPaneTabs();
    }

    internal void EnforceRightPaneTabs()
    {
        rightPaneGroup.DockWidth = new GridLength(RightPaneDockWidth);
        rightPaneGroup.FloatingWidth = RightPaneDockWidth;

        foreach (LayoutAnchorable anchorable in workspaceTools)
        {
            anchorable.CanClose = false;
            anchorable.CanFloat = false;
            anchorable.CanHide = CanUserHide(anchorable);
            anchorable.CanAutoHide = false;
            anchorable.AutoHideWidth = GetToolWidth(anchorable);
            anchorable.FloatingWidth = GetToolWidth(anchorable);
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
            ShowDockTool(cardsTool);
            pdfTool.Show();
            browserTool.Show();

            if (!inspectorTool.IsHidden)
            {
                ShowDockTool(inspectorTool);
            }

            if (!graphTool.IsHidden)
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

        if (IsFixedSideTool(anchorable))
        {
            ShowDockTool(anchorable);
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
        return !CanUserHide(anchorable);
    }

    internal bool RestoreWorkspaceToolIfOwned(LayoutAnchorable anchorable)
    {
        if (!IsWorkspaceTool(anchorable))
        {
            return false;
        }

        if (!CanUserHide(anchorable))
        {
            RestoreWorkspaceTool(anchorable);
            return true;
        }

        return false;
    }

    internal void RestoreHiddenWorkspaceTools()
    {
        foreach (LayoutAnchorable anchorable in workspaceTools)
        {
            if (anchorable.IsHidden && !CanUserHide(anchorable))
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
            ShowDockTool(anchorable);
        }, DispatcherPriority.Background);
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
            || ReferenceEquals(anchorable, pdfTool)
            || ReferenceEquals(anchorable, browserTool);
    }

    private bool CanUserHide(LayoutAnchorable anchorable)
    {
        return ReferenceEquals(anchorable, inspectorTool)
            || ReferenceEquals(anchorable, graphTool);
    }

    private bool IsFixedSideTool(LayoutAnchorable anchorable)
    {
        return ReferenceEquals(anchorable, cardsTool)
            || ReferenceEquals(anchorable, pdfTool)
            || ReferenceEquals(anchorable, browserTool);
    }

    private double GetToolWidth(LayoutAnchorable anchorable)
    {
        return CanUserHide(anchorable) ? InsightPaneDockWidth : SidePaneDockWidth;
    }

    private static void ShowDockTool(LayoutAnchorable anchorable)
    {
        anchorable.Show();
        anchorable.IsSelected = true;
        anchorable.IsActive = true;
    }
}
