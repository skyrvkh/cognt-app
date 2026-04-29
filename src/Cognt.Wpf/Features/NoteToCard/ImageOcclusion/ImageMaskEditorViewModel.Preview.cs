using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskEditorViewModel
{
    public bool TryValidateForSave(out string? message)
    {
        if (!File.Exists(Request.ImageFullPath))
        {
            message = "The image file no longer exists.";
            return false;
        }

        foreach (ImageOcclusionMask mask in CurrentImageSurface?.Masks ?? [])
        {
            if (!IsMaskValid(mask))
            {
                message = $"Mask '{mask.Label ?? mask.Id}' has invalid normalized coordinates.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mask.TargetId) && FindTargetById(mask.TargetId) is null)
            {
                message = $"Mask '{mask.Label ?? mask.Id}' is linked to a target that no longer exists.";
                return false;
            }
        }

        message = null;
        return true;
    }

    internal bool TryResolvePreviewVisualRole(ImageOcclusionMask mask, out ImageMaskVisualRole role)
    {
        return TryResolvePreviewMaskVisualState(mask, out role, out _);
    }

    internal bool TryResolvePreviewMaskVisualState(
        ImageOcclusionMask mask,
        out ImageMaskVisualRole role,
        out bool isCovered)
    {
        ArgumentNullException.ThrowIfNull(mask);

        role = ImageMaskVisualRole.HiddenTarget;
        isCovered = true;
        string? previewSlotId = GetActivePreviewSlotId();
        if (string.IsNullOrWhiteSpace(previewSlotId))
        {
            return false;
        }

        CardSlot? previewSlot = currentContext.Slots.FirstOrDefault(slot =>
            string.Equals(slot.Id, previewSlotId, StringComparison.Ordinal));
        CardPresentationPolicy presentation = CardPresentationPolicies.Resolve(currentContext, previewSlot);
        Dictionary<string, string> slotIdsByTargetId = currentContext.Targets
            .Where(target => !string.IsNullOrWhiteSpace(target.Id) && !string.IsNullOrWhiteSpace(target.SlotId))
            .ToDictionary(target => target.Id, target => target.SlotId, StringComparer.Ordinal);
        ImageMaskPreviewState? previewState = ImageMaskPreviewPolicy
            .Build([mask], slotIdsByTargetId, presentation, previewSlotId)
            .FirstOrDefault();
        if (previewState is null || !previewState.IsVisible)
        {
            return false;
        }

        role = previewState.Role;
        isCovered = previewState.IsCovered;
        return true;
    }

    private string? GetActivePreviewSlotId()
    {
        return PreviewSlotId
            ?? SelectedTargetOption?.SlotId
            ?? currentContext.Slots.OrderBy(slot => slot.DisplayClozeIndex).FirstOrDefault()?.Id;
    }

    private static bool IsMaskValid(ImageOcclusionMask mask)
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
