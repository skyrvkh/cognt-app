namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed partial class ImageMaskCoordinateMapper
{
    public ImageOcclusionMask ClampToImageBounds(ImageOcclusionMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        if (mask.Shape == ImageMaskShape.Ellipse)
        {
            double cx = ClampFinite(mask.Cx ?? (mask.X + (mask.Width / 2d)));
            double cy = ClampFinite(mask.Cy ?? (mask.Y + (mask.Height / 2d)));
            double rx = ClampFinite(mask.Rx ?? (mask.Width / 2d));
            double ry = ClampFinite(mask.Ry ?? (mask.Height / 2d));
            rx = Math.Min(rx, Math.Min(cx, 1d - cx));
            ry = Math.Min(ry, Math.Min(cy, 1d - cy));
            rx = Math.Max(0d, rx);
            ry = Math.Max(0d, ry);

            return mask with
            {
                X = Math.Max(0d, cx - rx),
                Y = Math.Max(0d, cy - ry),
                Width = Math.Max(0d, rx * 2d),
                Height = Math.Max(0d, ry * 2d),
                Cx = cx,
                Cy = cy,
                Rx = rx,
                Ry = ry,
            };
        }

        if (mask.Shape == ImageMaskShape.Polygon)
        {
            NormalizedPoint[] points = (mask.Points ?? [])
                .Select(point => new NormalizedPoint(ClampFinite(point.X), ClampFinite(point.Y)))
                .ToArray();
            double minX = points.Length == 0 ? 0d : points.Min(point => point.X);
            double minY = points.Length == 0 ? 0d : points.Min(point => point.Y);
            double maxX = points.Length == 0 ? minX : points.Max(point => point.X);
            double maxY = points.Length == 0 ? minY : points.Max(point => point.Y);

            return mask with
            {
                X = minX,
                Y = minY,
                Width = Math.Max(0d, maxX - minX),
                Height = Math.Max(0d, maxY - minY),
                Points = points,
            };
        }

        double x = ClampFinite(mask.X);
        double y = ClampFinite(mask.Y);
        double width = ClampFinite(mask.Width);
        double height = ClampFinite(mask.Height);

        if (x + width > 1d)
        {
            width = Math.Max(0d, 1d - x);
        }

        if (y + height > 1d)
        {
            height = Math.Max(0d, 1d - y);
        }

        return mask with
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
    }

    private static double ClampFinite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0d;
        }

        return Clamp(value, 0d, 1d);
    }
}
