using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas
{
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (Editor is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Point pointerPosition = e.GetPosition(this);
        if (SurfaceMode == ImageMaskSurfaceMode.Preview)
        {
            return;
        }

        if (Editor.CurrentTool == ImageMaskEditorTool.Select)
        {
            HandleSelectionPointerDown(pointerPosition, e);
            return;
        }

        if (!ContainsViewPoint(imageContentRect, pointerPosition))
        {
            return;
        }

        draftStartPoint = pointerPosition;
        draftRect = new Rect(pointerPosition, pointerPosition);
        isDrawing = true;
        CaptureMouse();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Editor is null)
        {
            return;
        }

        Point pointerPosition = e.GetPosition(this);
        if (isDrawing && draftStartPoint.HasValue)
        {
            draftRect = CreateDraftOverlayRect(
                draftStartPoint.Value,
                pointerPosition,
                constrainProportions: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (draggingMaskId is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            Vector dragOffset = pointerPosition - dragStartPoint;
            Rect nextRect = new(
                dragStartRect.X + dragOffset.X,
                dragStartRect.Y + dragOffset.Y,
                dragStartRect.Width,
                dragStartRect.Height);
            Editor.UpdateMaskBoundsFromViewRect(draggingMaskId, ImageMaskGeometryConverter.FromRect(nextRect), imageContentRect);
            RefreshOverlayVisuals();
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (Editor is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            CommitDraftMaskCreation();
            CompleteMaskMoveInteraction();
        }
        finally
        {
            draftStartPoint = null;
            ReleaseMouseCapture();
            RefreshOverlayVisuals();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (Editor is null || SurfaceMode == ImageMaskSurfaceMode.Preview)
        {
            return;
        }

        double nudgeStep = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.01d : 0.002d;
        switch (e.Key)
        {
            case Key.Delete:
                Editor.DeleteSelectedMask();
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Escape:
                if (TryCancelActiveOverlayInteraction())
                {
                    e.Handled = true;
                    return;
                }

                break;
            case Key.Left:
                Editor.MoveSelectedMaskBy(-nudgeStep, 0d);
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Right:
                Editor.MoveSelectedMaskBy(nudgeStep, 0d);
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Up:
                Editor.MoveSelectedMaskBy(0d, -nudgeStep);
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Down:
                Editor.MoveSelectedMaskBy(0d, nudgeStep);
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                Editor.Undo();
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
            case Key.Y when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                Editor.Redo();
                RefreshOverlayVisuals();
                e.Handled = true;
                return;
        }
    }
}
