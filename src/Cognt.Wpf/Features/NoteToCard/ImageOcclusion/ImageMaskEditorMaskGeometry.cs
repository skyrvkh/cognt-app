using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

internal static class ImageMaskEditorMaskGeometry
{
    internal static ImageOcclusionMask CreateDraft(ImageMaskShape shape)
    {
        return shape switch
        {
            ImageMaskShape.Ellipse => CardContextAuthoring.CreateEllipseMask(
                $"mask_{Guid.NewGuid():N}",
                targetId: null,
                cx: 0.5d,
                cy: 0.5d,
                rx: 0.1d,
                ry: 0.1d),
            _ => CardContextAuthoring.CreateRectangleMask(
                $"mask_{Guid.NewGuid():N}",
                targetId: null,
                x: 0d,
                y: 0d,
                width: 0d,
                height: 0d),
        };
    }

    internal static ImageOcclusionMask BuildMoved(ImageOcclusionMask existingMask, double deltaX, double deltaY)
    {
        return existingMask.Shape switch
        {
            ImageMaskShape.Ellipse => existingMask with
            {
                Cx = (existingMask.Cx ?? (existingMask.X + (existingMask.Width / 2d))) + deltaX,
                Cy = (existingMask.Cy ?? (existingMask.Y + (existingMask.Height / 2d))) + deltaY,
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
            },
            ImageMaskShape.Polygon => existingMask with
            {
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
                Points = existingMask.Points?.Select(point => new NormalizedPoint(
                    point.X + deltaX,
                    point.Y + deltaY)).ToArray(),
            },
            _ => existingMask with
            {
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
            },
        };
    }

    internal static bool HitTest(
        ImageOcclusionMask mask,
        ImageMaskViewPoint point,
        ImageMaskViewRect imageContentRect)
    {
        ImageMaskCoordinateMapper mapper = new();
        ImageMaskViewRect rect = mapper.MapNormalizedMaskToViewRect(mask, imageContentRect);
        if (rect.IsEmpty)
        {
            return false;
        }

        if (mask.Shape == ImageMaskShape.Ellipse)
        {
            double radiusX = rect.Width / 2d;
            double radiusY = rect.Height / 2d;
            if (radiusX <= 0d || radiusY <= 0d)
            {
                return false;
            }

            double centerX = rect.X + radiusX;
            double centerY = rect.Y + radiusY;
            double dx = (point.X - centerX) / radiusX;
            double dy = (point.Y - centerY) / radiusY;
            return (dx * dx) + (dy * dy) <= 1d;
        }

        return point.X >= rect.X
            && point.X <= rect.Right
            && point.Y >= rect.Y
            && point.Y <= rect.Bottom;
    }
}
