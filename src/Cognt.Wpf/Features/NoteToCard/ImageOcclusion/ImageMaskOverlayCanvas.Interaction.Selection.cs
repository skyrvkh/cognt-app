using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas
{
    private void HandleSelectionPointerDown(Point pointerPosition, MouseButtonEventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        if (Editor.TrySelectTopmostMaskAtPoint(new ImageMaskViewPoint(pointerPosition.X, pointerPosition.Y), imageContentRect))
        {
            draggingMaskId = Editor.SelectedMaskId;
            Rect? selectedRect = GetSelectedMaskOverlayRect();
            if (!selectedRect.HasValue)
            {
                return;
            }

            dragStartPoint = pointerPosition;
            dragStartRect = selectedRect.Value;
            Editor.BeginInteractiveChange();
            CaptureMouse();
            RefreshOverlayVisuals();
            e.Handled = true;
            return;
        }

        Editor.SetSelectedMask(null);
        RefreshOverlayVisuals();
    }

    private void CommitDraftMaskCreation()
    {
        if (Editor is null || !isDrawing)
        {
            return;
        }

        isDrawing = false;
        if (draftRect.Width <= 0d || draftRect.Height <= 0d)
        {
            return;
        }

        ImageMaskShape shape = Editor.CurrentTool == ImageMaskEditorTool.DrawEllipse
            ? ImageMaskShape.Ellipse
            : ImageMaskShape.Rect;
        Editor.CreateMaskFromViewRect(shape, ImageMaskGeometryConverter.FromRect(draftRect), imageContentRect);
    }

    private void CompleteMaskMoveInteraction()
    {
        if (Editor is null || draggingMaskId is null)
        {
            return;
        }

        Editor.CompleteInteractiveChange();
        draggingMaskId = null;
    }

    private bool TryCancelActiveOverlayInteraction()
    {
        if (Editor is null)
        {
            return false;
        }

        if (isDrawing)
        {
            isDrawing = false;
            draftStartPoint = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            return true;
        }

        if (draggingMaskId is null && !activeHandle.HasValue)
        {
            return false;
        }

        Editor.CancelInteractiveChange();
        draggingMaskId = null;
        activeHandle = null;
        resizeTotalDx = 0d;
        resizeTotalDy = 0d;
        ReleaseMouseCapture();
        RefreshOverlayVisuals();
        return true;
    }

    private static bool ContainsViewPoint(ImageMaskViewRect rect, Point point)
    {
        return point.X >= rect.X
            && point.X <= rect.Right
            && point.Y >= rect.Y
            && point.Y <= rect.Bottom;
    }
}
