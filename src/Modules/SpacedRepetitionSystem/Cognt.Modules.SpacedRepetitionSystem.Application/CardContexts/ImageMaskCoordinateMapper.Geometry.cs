namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed partial class ImageMaskCoordinateMapper
{
    private static ImageMaskViewRect ClampViewRectToImageContent(
        ImageMaskViewRect viewRect,
        ImageMaskViewRect imageContentRect)
    {
        double x = Clamp(viewRect.X, imageContentRect.X, imageContentRect.Right);
        double y = Clamp(viewRect.Y, imageContentRect.Y, imageContentRect.Bottom);
        double right = Clamp(viewRect.Right, imageContentRect.X, imageContentRect.Right);
        double bottom = Clamp(viewRect.Bottom, imageContentRect.Y, imageContentRect.Bottom);

        return new ImageMaskViewRect(
            Math.Min(x, right),
            Math.Min(y, bottom),
            Math.Max(0d, right - x),
            Math.Max(0d, bottom - y));
    }

    private static IReadOnlyList<NormalizedPoint>? ScalePolygonPoints(
        ImageOcclusionMask template,
        double normalizedX,
        double normalizedY,
        double normalizedWidth,
        double normalizedHeight)
    {
        if (template.Points is not { Count: > 0 })
        {
            return template.Points;
        }

        double sourceWidth = template.Width <= 0d ? 1d : template.Width;
        double sourceHeight = template.Height <= 0d ? 1d : template.Height;

        return template.Points
            .Select(point =>
            {
                double relativeX = (point.X - template.X) / sourceWidth;
                double relativeY = (point.Y - template.Y) / sourceHeight;
                return new NormalizedPoint(
                    normalizedX + (relativeX * normalizedWidth),
                    normalizedY + (relativeY * normalizedHeight));
            })
            .ToArray();
    }

    private static (double X, double Y, double Width, double Height) GetNormalizedBounds(ImageOcclusionMask mask)
    {
        return mask.Shape switch
        {
            ImageMaskShape.Ellipse when mask.Cx.HasValue && mask.Cy.HasValue && mask.Rx.HasValue && mask.Ry.HasValue
                => (
                    Math.Max(0d, mask.Cx.Value - mask.Rx.Value),
                    Math.Max(0d, mask.Cy.Value - mask.Ry.Value),
                    Math.Max(0d, mask.Rx.Value * 2d),
                    Math.Max(0d, mask.Ry.Value * 2d)),
            _ => (mask.X, mask.Y, mask.Width, mask.Height),
        };
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}
