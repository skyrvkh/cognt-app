using System.Windows;
using System.Windows.Media;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

internal static class ImageMaskGeometryConverter
{
    internal static Rect ToRect(ImageMaskViewRect rect)
    {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    internal static ImageMaskViewRect FromRect(Rect rect)
    {
        return new ImageMaskViewRect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    internal static PointCollection ToPointCollection(
        IReadOnlyList<NormalizedPoint>? points,
        ImageMaskViewRect imageContentRect,
        ImageMaskCoordinateMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        PointCollection collection = [];
        if (points is null)
        {
            return collection;
        }

        foreach (NormalizedPoint point in points)
        {
            ImageMaskViewPoint viewPoint = mapper.MapNormalizedPointToView(
                new ImageMaskViewPoint(point.X, point.Y),
                imageContentRect);
            collection.Add(new Point(viewPoint.X, viewPoint.Y));
        }

        return collection;
    }
}
