using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas
{
    private void HandleResizeHandleDragStarted(object sender, DragStartedEventArgs e)
    {
        if (Editor is null || sender is not Thumb thumb || thumb.Tag is not ResizeHandlePosition handle)
        {
            return;
        }

        Rect? rect = GetSelectedMaskOverlayRect();
        if (!rect.HasValue)
        {
            return;
        }

        activeHandle = handle;
        resizeStartRect = rect.Value;
        resizeTotalDx = 0d;
        resizeTotalDy = 0d;
        Editor.BeginInteractiveChange();
    }

    private void HandleResizeHandleDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Editor is null || activeHandle is null || Editor.SelectedMaskId is null)
        {
            return;
        }

        resizeTotalDx += e.HorizontalChange;
        resizeTotalDy += e.VerticalChange;
        Rect nextRect = CreateResizedOverlayRect(
            resizeStartRect,
            activeHandle.Value,
            resizeTotalDx,
            resizeTotalDy,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        Editor.UpdateMaskBoundsFromViewRect(Editor.SelectedMaskId, ImageMaskGeometryConverter.FromRect(nextRect), imageContentRect);
        RefreshOverlayVisuals();
        e.Handled = true;
    }

    private void HandleResizeHandleDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        activeHandle = null;
        resizeTotalDx = 0d;
        resizeTotalDy = 0d;
        Editor.CompleteInteractiveChange();
        RefreshOverlayVisuals();
    }
}
