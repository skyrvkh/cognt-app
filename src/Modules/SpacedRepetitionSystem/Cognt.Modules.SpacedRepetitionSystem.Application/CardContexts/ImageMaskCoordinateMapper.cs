namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed partial class ImageMaskCoordinateMapper
{
    public ImageMaskViewRect GetDisplayedImageRect(
        ImageMaskViewSize controlSize,
        ImageMaskViewSize imagePixelSize)
    {
        if (controlSize.Width <= 0d
            || controlSize.Height <= 0d
            || imagePixelSize.Width <= 0d
            || imagePixelSize.Height <= 0d)
        {
            return default;
        }

        double controlAspect = controlSize.Width / controlSize.Height;
        double imageAspect = imagePixelSize.Width / imagePixelSize.Height;

        if (controlAspect >= imageAspect)
        {
            double height = controlSize.Height;
            double width = height * imageAspect;
            return new ImageMaskViewRect(
                (controlSize.Width - width) / 2d,
                0d,
                width,
                height);
        }

        double contentWidth = controlSize.Width;
        double contentHeight = contentWidth / imageAspect;
        return new ImageMaskViewRect(
            0d,
            (controlSize.Height - contentHeight) / 2d,
            contentWidth,
            contentHeight);
    }

    public ImageMaskViewPoint MapNormalizedPointToView(
        ImageMaskViewPoint normalizedPoint,
        ImageMaskViewRect imageContentRect)
    {
        if (imageContentRect.IsEmpty)
        {
            return default;
        }

        return new ImageMaskViewPoint(
            imageContentRect.X + (normalizedPoint.X * imageContentRect.Width),
            imageContentRect.Y + (normalizedPoint.Y * imageContentRect.Height));
    }

    public ImageMaskViewPoint MapViewPointToNormalizedImage(
        ImageMaskViewPoint viewPoint,
        ImageMaskViewRect imageContentRect)
    {
        if (imageContentRect.IsEmpty)
        {
            return default;
        }

        double clampedX = Clamp(viewPoint.X, imageContentRect.X, imageContentRect.Right);
        double clampedY = Clamp(viewPoint.Y, imageContentRect.Y, imageContentRect.Bottom);

        return new ImageMaskViewPoint(
            imageContentRect.Width <= 0d ? 0d : (clampedX - imageContentRect.X) / imageContentRect.Width,
            imageContentRect.Height <= 0d ? 0d : (clampedY - imageContentRect.Y) / imageContentRect.Height);
    }
}
