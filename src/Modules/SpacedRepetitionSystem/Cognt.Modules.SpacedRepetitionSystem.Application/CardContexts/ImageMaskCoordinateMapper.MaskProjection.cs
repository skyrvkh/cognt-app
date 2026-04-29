namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed partial class ImageMaskCoordinateMapper
{
    public ImageMaskViewRect MapNormalizedMaskToViewRect(
        ImageOcclusionMask mask,
        ImageMaskViewRect imageContentRect)
    {
        ArgumentNullException.ThrowIfNull(mask);

        if (imageContentRect.IsEmpty)
        {
            return default;
        }

        (double x, double y, double width, double height) = GetNormalizedBounds(mask);
        return new ImageMaskViewRect(
            imageContentRect.X + (x * imageContentRect.Width),
            imageContentRect.Y + (y * imageContentRect.Height),
            width * imageContentRect.Width,
            height * imageContentRect.Height);
    }

    public ImageOcclusionMask MapViewRectToImageMask(
        ImageOcclusionMask template,
        ImageMaskViewRect viewRect,
        ImageMaskViewRect imageContentRect)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (imageContentRect.IsEmpty)
        {
            return ClampToImageBounds(template);
        }

        ImageMaskViewRect clampedRect = ClampViewRectToImageContent(viewRect, imageContentRect);
        double normalizedX = (clampedRect.X - imageContentRect.X) / imageContentRect.Width;
        double normalizedY = (clampedRect.Y - imageContentRect.Y) / imageContentRect.Height;
        double normalizedWidth = clampedRect.Width / imageContentRect.Width;
        double normalizedHeight = clampedRect.Height / imageContentRect.Height;

        ImageOcclusionMask updated = template.Shape switch
        {
            ImageMaskShape.Ellipse => template with
            {
                X = normalizedX,
                Y = normalizedY,
                Width = normalizedWidth,
                Height = normalizedHeight,
                Cx = normalizedX + (normalizedWidth / 2d),
                Cy = normalizedY + (normalizedHeight / 2d),
                Rx = normalizedWidth / 2d,
                Ry = normalizedHeight / 2d,
            },
            ImageMaskShape.Polygon => template with
            {
                X = normalizedX,
                Y = normalizedY,
                Width = normalizedWidth,
                Height = normalizedHeight,
                Points = ScalePolygonPoints(template, normalizedX, normalizedY, normalizedWidth, normalizedHeight),
            },
            _ => template with
            {
                X = normalizedX,
                Y = normalizedY,
                Width = normalizedWidth,
                Height = normalizedHeight,
            },
        };

        return ClampToImageBounds(updated);
    }
}
