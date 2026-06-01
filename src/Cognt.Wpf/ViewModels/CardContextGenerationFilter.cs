using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static class CardContextGenerationFilter
{
    internal static CardContext Filter(CardContext context, IReadOnlySet<string> slotIds)
    {
        var slots = new List<CardSlot>();
        var selectedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        var selectedTargetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (CardSlot slot in context.Slots)
        {
            if (!slotIds.Contains(slot.Id))
            {
                continue;
            }

            slots.Add(slot);
            selectedSlotIds.Add(slot.Id);

            foreach (string targetId in slot.TargetIds)
            {
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    selectedTargetIds.Add(targetId);
                }
            }
        }

        foreach (CardTarget target in context.Targets)
        {
            if (selectedSlotIds.Contains(target.SlotId))
            {
                selectedTargetIds.Add(target.Id);
            }
        }

        var targets = new List<CardTarget>();
        var selectedMaskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CardTarget target in context.Targets)
        {
            if (!selectedTargetIds.Contains(target.Id))
            {
                continue;
            }

            targets.Add(target);

            foreach (ImageMaskAnchor anchor in target.ImageAnchors)
            {
                selectedMaskIds.Add(anchor.MaskId);
            }
        }

        var images = new List<CardContextImageSurface>();
        foreach (CardContextImageSurface image in context.Images)
        {
            var masks = new List<ImageOcclusionMask>();
            foreach (ImageOcclusionMask mask in image.Masks)
            {
                if (selectedTargetIds.Contains(mask.TargetId) || selectedMaskIds.Contains(mask.Id))
                {
                    masks.Add(mask);
                }
            }

            if (masks.Count > 0)
            {
                images.Add(image with { Masks = masks });
            }
        }

        return context with
        {
            Slots = slots,
            Targets = targets,
            Images = images,
        };
    }
}
