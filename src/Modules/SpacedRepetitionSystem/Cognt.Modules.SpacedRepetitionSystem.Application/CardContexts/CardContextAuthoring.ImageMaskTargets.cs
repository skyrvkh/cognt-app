namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext AddImageMaskTargetToNewSlot(
        CardContext context,
        string imageBlockId,
        string imageRelPath,
        ImageOcclusionMask mask,
        string answer,
        string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mask);

        CardSlot slot = CreateNewSlot(GetNextDisplayClozeIndex(context));
        CardTarget target = CreateCardTarget(slot.Id, answer, hint);
        ImageOcclusionMask linkedMask = mask with
        {
            TargetId = target.Id,
            Label = string.IsNullOrWhiteSpace(mask.Label) ? NormalizeInlineText(answer) : mask.Label,
        };

        return AttachImageMaskToTarget(
            AddTargetToExistingSlot(
                AddSlot(context, slot),
                slot.Id,
                target),
            target.Id,
            CreateImageMaskAnchor(imageBlockId, linkedMask.Id),
            linkedMask,
            imageRelPath);
    }
}
