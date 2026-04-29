using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas
{
    private void DrawMask(DrawingContext drawingContext, ImageMaskItemViewModel item, ImageMaskVisualRole role, bool isCovered)
    {
        Rect rect = ImageMaskGeometryConverter.ToRect(coordinateMapper.MapNormalizedMaskToViewRect(item.Mask, imageContentRect));
        if (rect.IsEmpty)
        {
            return;
        }

        Brush? fill = isCovered ? ResolveMaskBrush(role, isStroke: false) : null;
        Pen stroke = new(ResolveMaskBrush(role, isStroke: true), item.IsSelected ? 2d : 1.5d);
        stroke.Freeze();

        switch (item.Shape)
        {
            case ImageMaskShape.Ellipse:
                drawingContext.DrawEllipse(fill, stroke, rect.Location + new Vector(rect.Width / 2d, rect.Height / 2d), rect.Width / 2d, rect.Height / 2d);
                break;
            case ImageMaskShape.Polygon:
                DrawPolygonMask(drawingContext, item, rect, fill, stroke, isCovered);
                break;
            default:
                drawingContext.DrawRectangle(fill, stroke, rect);
                break;
        }

        DrawMaskLabel(drawingContext, item, rect);
    }

    private void DrawDraftMask(DrawingContext drawingContext)
    {
        Brush fill = ResolveDraftMaskBrush();
        Pen stroke = new(ResolveDraftMaskStrokeBrush(), 1.5d);
        stroke.Freeze();

        if (Editor?.CurrentTool == ImageMaskEditorTool.DrawEllipse)
        {
            drawingContext.DrawEllipse(fill, stroke, draftRect.Location + new Vector(draftRect.Width / 2d, draftRect.Height / 2d), draftRect.Width / 2d, draftRect.Height / 2d);
            return;
        }

        drawingContext.DrawRectangle(fill, stroke, draftRect);
    }

    private void DrawPolygonMask(
        DrawingContext drawingContext,
        ImageMaskItemViewModel item,
        Rect rect,
        Brush? fill,
        Pen stroke,
        bool isCovered)
    {
        PointCollection points = ImageMaskGeometryConverter.ToPointCollection(item.Mask.Points, imageContentRect, coordinateMapper);
        if (points.Count < 3)
        {
            drawingContext.DrawRectangle(fill, stroke, rect);
            return;
        }

        StreamGeometry geometry = new();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(points[0], isFilled: isCovered, isClosed: true);
        context.PolyLineTo(points.Skip(1).ToArray(), isStroked: true, isSmoothJoin: true);
        geometry.Freeze();
        drawingContext.DrawGeometry(fill, stroke, geometry);
    }

    private void DrawMaskLabel(DrawingContext drawingContext, ImageMaskItemViewModel item, Rect rect)
    {
        if (string.IsNullOrWhiteSpace(item.Label))
        {
            return;
        }

        FormattedText text = new(
            item.Label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12d,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(text, new Point(rect.X + 6d, rect.Y + 4d));
    }

    private Brush ResolveMaskBrush(ImageMaskVisualRole role, bool isStroke)
    {
        string key = role switch
        {
            ImageMaskVisualRole.CurrentTarget => isStroke ? "ImageMask.CurrentTarget.Stroke" : "ImageMask.CurrentTarget.Fill",
            ImageMaskVisualRole.SiblingTarget => isStroke ? "ImageMask.SiblingTarget.Stroke" : "ImageMask.SiblingTarget.Fill",
            ImageMaskVisualRole.HiddenTarget => isStroke ? "ImageMask.HiddenTarget.Stroke" : "ImageMask.HiddenTarget.Fill",
            ImageMaskVisualRole.ResolvedTarget => isStroke ? "ImageMask.ResolvedTarget.Stroke" : "ImageMask.ResolvedTarget.Fill",
            ImageMaskVisualRole.Selected => isStroke ? "ImageMask.Selected.Stroke" : "ImageMask.Selected.Fill",
            ImageMaskVisualRole.Invalid => isStroke ? "ImageMask.Invalid.Stroke" : "ImageMask.Invalid.Fill",
            ImageMaskVisualRole.Disabled => isStroke ? "ImageMask.Disabled.Stroke" : "ImageMask.Disabled.Fill",
            _ => isStroke ? "ImageMask.Draft.Stroke" : "ImageMask.Draft.Fill",
        };

        return TryFindResource(key) as Brush
            ?? (isStroke ? Brushes.White : new SolidColorBrush(Color.FromArgb(120, 99, 102, 241)));
    }

    private Brush ResolveDraftMaskBrush()
    {
        return TryFindResource("ImageMask.Draft.Fill") as Brush
            ?? new SolidColorBrush(Color.FromArgb(96, 72, 97, 181));
    }

    private Brush ResolveDraftMaskStrokeBrush()
    {
        return TryFindResource("ImageMask.Draft.Stroke") as Brush
            ?? Brushes.White;
    }
}
