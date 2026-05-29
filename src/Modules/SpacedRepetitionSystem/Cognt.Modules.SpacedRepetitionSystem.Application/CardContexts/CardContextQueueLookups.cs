namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal sealed class CardContextQueueLookups
{
    private readonly IReadOnlyDictionary<string, CardTarget> targetsById;
    private readonly IReadOnlyDictionary<string, List<CardTarget>> targetsBySlotId;

    private CardContextQueueLookups(
        IReadOnlySet<int> duplicateDisplayIndexes,
        IReadOnlyDictionary<string, CardTarget> targetsById,
        IReadOnlyDictionary<string, List<CardTarget>> targetsBySlotId,
        IReadOnlyDictionary<string, CardContextImageSurface> imagesByBlockId,
        IReadOnlyDictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> masksByImageAndId)
    {
        DuplicateDisplayIndexes = duplicateDisplayIndexes;
        this.targetsById = targetsById;
        this.targetsBySlotId = targetsBySlotId;
        ImagesByBlockId = imagesByBlockId;
        MasksByImageAndId = masksByImageAndId;
    }

    internal IReadOnlySet<int> DuplicateDisplayIndexes { get; }

    internal IReadOnlyDictionary<string, CardContextImageSurface> ImagesByBlockId { get; }

    internal IReadOnlyDictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> MasksByImageAndId { get; }

    internal static CardContextQueueLookups Create(CardContext context)
    {
        return new CardContextQueueLookups(
            FindDuplicateDisplayIndexes(context.Slots),
            BuildTargetLookup(context.Targets),
            BuildTargetsBySlotLookup(context.Targets),
            BuildImageSurfaceLookup(context.Images),
            BuildImageMaskLookup(context.Images));
    }

    internal IReadOnlyList<CardTarget> ResolveEnabledTargets(CardSlot slot)
    {
        if (slot.TargetIds.Count == 0)
        {
            return targetsBySlotId.TryGetValue(slot.Id, out List<CardTarget>? slotTargets)
                ? slotTargets.Where(static target => !target.IsDisabled).ToArray()
                : [];
        }

        return slot.TargetIds
            .Select(targetId => targetsById.TryGetValue(targetId, out CardTarget? target) ? target : null)
            .OfType<CardTarget>()
            .Where(static target => !target.IsDisabled)
            .ToArray();
    }

    private static Dictionary<string, CardTarget> BuildTargetLookup(IReadOnlyList<CardTarget> targets)
    {
        return targets
            .DistinctBy(static target => target.Id)
            .ToDictionary(static target => target.Id, StringComparer.Ordinal);
    }

    private static Dictionary<string, List<CardTarget>> BuildTargetsBySlotLookup(IReadOnlyList<CardTarget> targets)
    {
        return targets
            .GroupBy(static target => target.SlotId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);
    }

    private static Dictionary<string, CardContextImageSurface> BuildImageSurfaceLookup(IReadOnlyList<CardContextImageSurface> images)
    {
        return images
            .DistinctBy(static image => image.ImageBlockId)
            .ToDictionary(static image => image.ImageBlockId, StringComparer.Ordinal);
    }

    private static Dictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> BuildImageMaskLookup(
        IReadOnlyList<CardContextImageSurface> images)
    {
        return images
            .SelectMany(static image => image.Masks.Select(mask => (Key: (image.ImageBlockId, mask.Id), Mask: mask)))
            .DistinctBy(static entry => entry.Key)
            .ToDictionary(static entry => entry.Key, static entry => entry.Mask);
    }

    private static IReadOnlySet<int> FindDuplicateDisplayIndexes(IReadOnlyList<CardSlot> slots)
    {
        return slots
            .GroupBy(static slot => slot.DisplayClozeIndex)
            .Where(static group => group.Skip(1).Any())
            .Select(static group => group.Key)
            .ToHashSet();
    }
}
