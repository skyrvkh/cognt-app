using System.Windows;
using AvalonDock.Layout;

namespace CogntApp.Shell.Workspace;

internal sealed class NoteDocumentDragController
{
    private const double DragThreshold = 8;

    private LayoutDocument? document;
    private Point startScreenPoint;

    internal void Begin(LayoutDocument layoutDocument, Point screenPoint)
    {
        document = layoutDocument;
        startScreenPoint = screenPoint;
    }

    internal void Reset()
    {
        document = null;
        startScreenPoint = default;
    }

    internal bool ShouldDetach(LayoutDocument layoutDocument, Point screenPoint, Rect workspaceScreenBounds)
    {
        if (!ReferenceEquals(document, layoutDocument))
        {
            return false;
        }

        if (workspaceScreenBounds.Contains(screenPoint))
        {
            return false;
        }

        double horizontalDelta = Math.Abs(screenPoint.X - startScreenPoint.X);
        double verticalDelta = Math.Abs(screenPoint.Y - startScreenPoint.Y);
        return horizontalDelta >= DragThreshold || verticalDelta >= DragThreshold;
    }
}
