using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

internal static class ImageMaskEditorMaskState
{
    internal static ImageMaskVisualRole ResolveVisualRole(
        ImageOcclusionMask mask,
        bool isInvalid,
        bool isSelected)
    {
        if (mask.IsDisabled)
        {
            return ImageMaskVisualRole.Disabled;
        }

        if (isSelected)
        {
            return ImageMaskVisualRole.Selected;
        }

        if (isInvalid)
        {
            return ImageMaskVisualRole.Invalid;
        }

        return string.IsNullOrWhiteSpace(mask.TargetId)
            ? ImageMaskVisualRole.Draft
            : ImageMaskVisualRole.ResolvedTarget;
    }

    internal static bool IsValid(ImageOcclusionMask mask)
    {
        static bool IsNormalized(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 1d;
        }

        return mask.Shape switch
        {
            ImageMaskShape.Ellipse => mask.Cx.HasValue
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
                                      && mask.Cy.Value + mask.Ry.Value <= 1d,
            ImageMaskShape.Polygon => mask.Points is { Count: >= 3 }
                                      && mask.Points.All(point => IsNormalized(point.X) && IsNormalized(point.Y))
                                      && mask.Width > 0d
                                      && mask.Height > 0d,
            _ => IsNormalized(mask.X)
                 && IsNormalized(mask.Y)
                 && IsNormalized(mask.Width)
                 && IsNormalized(mask.Height)
                 && mask.Width > 0d
                 && mask.Height > 0d
                 && mask.X + mask.Width <= 1d
                 && mask.Y + mask.Height <= 1d,
        };
    }
}
