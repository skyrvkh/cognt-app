using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas
{
    private void RefreshResizeHandles()
    {
        Children.Clear();

        if (Editor is null || SurfaceMode == ImageMaskSurfaceMode.Preview)
        {
            return;
        }

        Rect? rect = GetSelectedMaskOverlayRect();
        if (!rect.HasValue)
        {
            return;
        }

        foreach ((ResizeHandlePosition Handle, Point Center, Cursor Cursor) handle in GetResizeHandleLayout(rect.Value))
        {
            Thumb thumb = new()
            {
                Width = 10d,
                Height = 10d,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1d),
                Cursor = handle.Cursor,
                Tag = handle.Handle,
            };
            thumb.DragStarted += HandleResizeHandleDragStarted;
            thumb.DragDelta += HandleResizeHandleDragDelta;
            thumb.DragCompleted += HandleResizeHandleDragCompleted;
            SetLeft(thumb, handle.Center.X - (thumb.Width / 2d));
            SetTop(thumb, handle.Center.Y - (thumb.Height / 2d));
            Children.Add(thumb);
        }
    }

    private Rect? GetSelectedMaskOverlayRect()
    {
        if (Editor?.SelectedMask is null)
        {
            return null;
        }

        return ImageMaskGeometryConverter.ToRect(
            coordinateMapper.MapNormalizedMaskToViewRect(Editor.SelectedMask.Mask, imageContentRect));
    }

    private static IEnumerable<(ResizeHandlePosition Handle, Point Center, Cursor Cursor)> GetResizeHandleLayout(Rect rect)
    {
        yield return (ResizeHandlePosition.TopLeft, rect.TopLeft, Cursors.SizeNWSE);
        yield return (ResizeHandlePosition.Top, new Point(rect.Left + (rect.Width / 2d), rect.Top), Cursors.SizeNS);
        yield return (ResizeHandlePosition.TopRight, rect.TopRight, Cursors.SizeNESW);
        yield return (ResizeHandlePosition.Right, new Point(rect.Right, rect.Top + (rect.Height / 2d)), Cursors.SizeWE);
        yield return (ResizeHandlePosition.BottomRight, rect.BottomRight, Cursors.SizeNWSE);
        yield return (ResizeHandlePosition.Bottom, new Point(rect.Left + (rect.Width / 2d), rect.Bottom), Cursors.SizeNS);
        yield return (ResizeHandlePosition.BottomLeft, rect.BottomLeft, Cursors.SizeNESW);
        yield return (ResizeHandlePosition.Left, new Point(rect.Left, rect.Top + (rect.Height / 2d)), Cursors.SizeWE);
    }

    private static Rect CreateResizedOverlayRect(
        Rect rect,
        ResizeHandlePosition handle,
        double dx,
        double dy,
        bool constrainProportions)
    {
        Rect next = rect;
        switch (handle)
        {
            case ResizeHandlePosition.TopLeft:
                next = new Rect(rect.Left + dx, rect.Top + dy, rect.Width - dx, rect.Height - dy);
                break;
            case ResizeHandlePosition.Top:
                next = new Rect(rect.Left, rect.Top + dy, rect.Width, rect.Height - dy);
                break;
            case ResizeHandlePosition.TopRight:
                next = new Rect(rect.Left, rect.Top + dy, rect.Width + dx, rect.Height - dy);
                break;
            case ResizeHandlePosition.Right:
                next = new Rect(rect.Left, rect.Top, rect.Width + dx, rect.Height);
                break;
            case ResizeHandlePosition.BottomRight:
                next = new Rect(rect.Left, rect.Top, rect.Width + dx, rect.Height + dy);
                break;
            case ResizeHandlePosition.Bottom:
                next = new Rect(rect.Left, rect.Top, rect.Width, rect.Height + dy);
                break;
            case ResizeHandlePosition.BottomLeft:
                next = new Rect(rect.Left + dx, rect.Top, rect.Width - dx, rect.Height + dy);
                break;
            case ResizeHandlePosition.Left:
                next = new Rect(rect.Left + dx, rect.Top, rect.Width - dx, rect.Height);
                break;
        }

        if (constrainProportions)
        {
            double size = Math.Max(Math.Abs(next.Width), Math.Abs(next.Height));
            next = new Rect(next.X, next.Y, size, size);
        }

        return new Rect(
            Math.Min(next.Left, next.Right),
            Math.Min(next.Top, next.Bottom),
            Math.Max(0d, Math.Abs(next.Width)),
            Math.Max(0d, Math.Abs(next.Height)));
    }

    private static Rect CreateDraftOverlayRect(Point start, Point end, bool constrainProportions)
    {
        Vector delta = end - start;
        if (constrainProportions)
        {
            double size = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            delta = new Vector(Math.Sign(delta.X) * size, Math.Sign(delta.Y) * size);
            end = start + delta;
        }

        return new Rect(start, end);
    }

    private enum ResizeHandlePosition
    {
        TopLeft = 0,
        Top = 1,
        TopRight = 2,
        Right = 3,
        BottomRight = 4,
        Bottom = 5,
        BottomLeft = 6,
        Left = 7,
    }
}
