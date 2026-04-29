namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static ImageMaskAnchor CreateImageMaskAnchor(string imageBlockId, string maskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBlockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskId);

        return new ImageMaskAnchor(NewStableId("anchor"), imageBlockId.Trim(), maskId.Trim());
    }

    public static ImageOcclusionMask CreateRectangleMask(
        string id,
        string? targetId,
        double x,
        double y,
        double width,
        double height,
        string? label = null,
        bool isDisabled = false)
    {
        return new ImageOcclusionMask(
            id,
            NormalizeOptionalId(targetId),
            ImageMaskShape.Rect,
            x,
            y,
            width,
            height,
            null,
            null,
            null,
            null,
            null,
            NormalizeOptionalText(label),
            isDisabled);
    }

    public static ImageOcclusionMask CreateEllipseMask(
        string id,
        string? targetId,
        double cx,
        double cy,
        double rx,
        double ry,
        string? label = null,
        bool isDisabled = false)
    {
        return new ImageOcclusionMask(
            id,
            NormalizeOptionalId(targetId),
            ImageMaskShape.Ellipse,
            Math.Max(0d, cx - rx),
            Math.Max(0d, cy - ry),
            Math.Max(0d, rx * 2d),
            Math.Max(0d, ry * 2d),
            cx,
            cy,
            rx,
            ry,
            null,
            NormalizeOptionalText(label),
            isDisabled);
    }

    public static ImageOcclusionMask CreatePolygonMask(
        string id,
        string? targetId,
        IReadOnlyList<NormalizedPoint> points,
        string? label = null,
        bool isDisabled = false)
    {
        ArgumentNullException.ThrowIfNull(points);

        double minX = points.Count == 0 ? 0d : points.Min(point => point.X);
        double minY = points.Count == 0 ? 0d : points.Min(point => point.Y);
        double maxX = points.Count == 0 ? 0d : points.Max(point => point.X);
        double maxY = points.Count == 0 ? 0d : points.Max(point => point.Y);

        return new ImageOcclusionMask(
            id,
            NormalizeOptionalId(targetId),
            ImageMaskShape.Polygon,
            minX,
            minY,
            Math.Max(0d, maxX - minX),
            Math.Max(0d, maxY - minY),
            null,
            null,
            null,
            null,
            points.ToArray(),
            NormalizeOptionalText(label),
            isDisabled);
    }

    private static string NormalizeOptionalId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
