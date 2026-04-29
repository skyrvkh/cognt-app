namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed record ImageMaskPreviewState(
    string MaskId,
    string TargetId,
    string? SlotId,
    bool IsVisible,
    bool IsCovered,
    ImageMaskVisualRole Role);

public static class ImageMaskPreviewPolicy
{
    public static IReadOnlyList<ImageMaskPreviewState> Build(
        IReadOnlyList<ImageOcclusionMask> masks,
        IReadOnlyDictionary<string, string> slotIdsByTargetId,
        CardCueMode cueMode,
        string currentSlotId)
    {
        CardPresentationPolicy policy = CardPresentationPolicies.FromLegacyCueModes(null, cueMode)
            ?? CardPresentationPolicies.AppDefault;
        return Build(masks, slotIdsByTargetId, policy, currentSlotId);
    }

    public static IReadOnlyList<ImageMaskPreviewState> Build(
        IReadOnlyList<ImageOcclusionMask> masks,
        IReadOnlyDictionary<string, string> slotIdsByTargetId,
        CardPresentationPolicy policy,
        string currentSlotId)
    {
        ArgumentNullException.ThrowIfNull(masks);
        ArgumentNullException.ThrowIfNull(slotIdsByTargetId);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSlotId);

        List<ImageMaskPreviewState> states = [];
        foreach (ImageOcclusionMask mask in masks.Where(candidate => !candidate.IsDisabled))
        {
            slotIdsByTargetId.TryGetValue(mask.TargetId, out string? slotId);
            bool isCurrent = string.Equals(slotId, currentSlotId, StringComparison.Ordinal);

            switch (policy.ImageFrontMode)
            {
                case ImageOcclusionFrontMode.None:
                case ImageOcclusionFrontMode.ShowNoMasks:
                    break;

                case ImageOcclusionFrontMode.HideCurrentMaskOnly:
                    if (!isCurrent)
                    {
                        continue;
                    }

                    states.Add(new ImageMaskPreviewState(
                        mask.Id,
                        mask.TargetId,
                        slotId,
                        IsVisible: true,
                        IsCovered: true,
                        policy.CueCurrentTarget ? ImageMaskVisualRole.CurrentTarget : ImageMaskVisualRole.HiddenTarget));
                    break;

                case ImageOcclusionFrontMode.HideAllMasksCueCurrent:
                    states.Add(new ImageMaskPreviewState(
                        mask.Id,
                        mask.TargetId,
                        slotId,
                        IsVisible: true,
                        IsCovered: true,
                        ResolveHiddenRole(isCurrent, policy.CueCurrentTarget)));
                    break;

                case ImageOcclusionFrontMode.ShowVisibleMasksAsOutlines:
                    states.Add(new ImageMaskPreviewState(
                        mask.Id,
                        mask.TargetId,
                        slotId,
                        IsVisible: true,
                        IsCovered: false,
                        isCurrent ? ImageMaskVisualRole.CurrentTarget : ImageMaskVisualRole.SiblingTarget));
                    break;

                case ImageOcclusionFrontMode.HideAllMasksSameStyle:
                default:
                    states.Add(new ImageMaskPreviewState(
                        mask.Id,
                        mask.TargetId,
                        slotId,
                        IsVisible: true,
                        IsCovered: true,
                        ImageMaskVisualRole.HiddenTarget));
                    break;
            }
        }

        return states;
    }

    private static ImageMaskVisualRole ResolveHiddenRole(bool isCurrent, bool cueCurrentTarget)
    {
        if (isCurrent)
        {
            return cueCurrentTarget ? ImageMaskVisualRole.CurrentTarget : ImageMaskVisualRole.HiddenTarget;
        }

        return cueCurrentTarget ? ImageMaskVisualRole.SiblingTarget : ImageMaskVisualRole.HiddenTarget;
    }
}
