namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static bool HasValidImageMaskCoordinates(ImageOcclusionMask mask)
    {
        if (mask.Shape == ImageMaskShape.Ellipse)
        {
            return mask.Cx.HasValue
                && mask.Cy.HasValue
                && mask.Rx.HasValue
                && mask.Ry.HasValue
                && IsNormalized(mask.Cx.Value)
                && IsNormalized(mask.Cy.Value)
                && IsNormalized(mask.Rx.Value)
                && IsNormalized(mask.Ry.Value)
                && mask.Rx.Value > 0d
                && mask.Ry.Value > 0d
                && mask.Cx.Value - mask.Rx.Value >= 0d
                && mask.Cx.Value + mask.Rx.Value <= 1d
                && mask.Cy.Value - mask.Ry.Value >= 0d
                && mask.Cy.Value + mask.Ry.Value <= 1d;
        }

        if (mask.Shape == ImageMaskShape.Polygon)
        {
            return mask.Points is { Count: >= 3 }
                && mask.Points.All(point => IsNormalized(point.X) && IsNormalized(point.Y))
                && IsNormalized(mask.X)
                && IsNormalized(mask.Y)
                && IsNormalized(mask.Width)
                && IsNormalized(mask.Height)
                && mask.Width > 0d
                && mask.Height > 0d
                && mask.X + mask.Width <= 1d
                && mask.Y + mask.Height <= 1d;
        }

        return IsNormalized(mask.X)
            && IsNormalized(mask.Y)
            && IsNormalized(mask.Width)
            && IsNormalized(mask.Height)
            && mask.Width > 0d
            && mask.Height > 0d
            && mask.X + mask.Width <= 1d
            && mask.Y + mask.Height <= 1d;
    }

    private static bool IsNormalized(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 1d;
    }
}
