using System.Security.Cryptography;
using System.Text;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextSnapshotFactory
{
    internal static string ComputeSnapshotHash(string contextContent, CardContext context)
    {
        string payload = string.Join(
            "\n",
            context.Id,
            context.Title ?? string.Empty,
            context.Order.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FormatPolicy(ResolvePresentationPolicy(context)),
            contextContent ?? string.Empty,
            string.Join("|", context.ContextBlockIds.Order(StringComparer.Ordinal)),
            string.Join(
                "|",
                context.Slots
                    .OrderBy(slot => slot.DisplayClozeIndex)
                    .ThenBy(slot => slot.Id, StringComparer.Ordinal)
                    .Select(slot => $"{slot.Id}:{slot.DisplayClozeIndex}:{slot.IsDisabled}:{slot.Order}:{FormatPolicy(slot.PresentationOverride)}:{string.Join(",", slot.TargetIds.Order(StringComparer.Ordinal))}")),
            string.Join(
                "|",
                context.Targets
                    .OrderBy(target => target.SlotId, StringComparer.Ordinal)
                    .ThenBy(target => target.Id, StringComparer.Ordinal)
                    .Select(target =>
                        $"{target.Id}:{target.SlotId}:{target.Answer}:{target.Hint}:{target.IsDisabled}:{string.Join(",", target.TextAnchors.OrderBy(anchor => anchor.Id, StringComparer.Ordinal).Select(anchor => $"{anchor.Id}:{anchor.BlockId}:{anchor.MarkId}:{anchor.RangeId}:{anchor.SelectedTextHash}:{anchor.SelectedTextPreview}"))}:{string.Join(",", target.ImageAnchors.OrderBy(anchor => anchor.Id, StringComparer.Ordinal).Select(anchor => $"{anchor.Id}:{anchor.ImageBlockId}:{anchor.MaskId}"))}")),
            string.Join(
                "|",
                context.Images
                    .OrderBy(image => image.ImageBlockId, StringComparer.Ordinal)
                    .Select(image =>
                        $"{image.ImageBlockId}:{image.ImageRelPath}:{string.Join(",", image.Masks.OrderBy(mask => mask.Id, StringComparer.Ordinal).Select(mask => $"{mask.Id}:{mask.TargetId}:{mask.Shape}:{mask.X}:{mask.Y}:{mask.Width}:{mask.Height}:{mask.Cx}:{mask.Cy}:{mask.Rx}:{mask.Ry}:{FormatPoints(mask.Points)}:{mask.Label}:{mask.IsDisabled}"))}")));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    internal static string ComputeSlotSnapshotHash(CardContextSnapshot snapshot, string slotId)
    {
        CardSlot slot = snapshot.Slots.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, slotId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The card slot '{slotId}' does not exist in snapshot '{snapshot.Id}'.");
        IReadOnlyList<CardTarget> slotTargets = slot.TargetIds.Count == 0
            ? snapshot.Targets
                .Where(target => string.Equals(target.SlotId, slot.Id, StringComparison.Ordinal))
                .ToArray()
            : snapshot.Targets
                .Where(target => slot.TargetIds.Contains(target.Id, StringComparer.Ordinal))
                .ToArray();

        string payload = string.Join(
            "\n",
            snapshot.SourceRelPath,
            snapshot.Id,
            slot.Id,
            FormatPolicy(ResolvePresentationPolicy(snapshot, slot)),
            snapshot.ContextContent ?? string.Empty,
            string.Join(
                "|",
                snapshot.Slots
                    .OrderBy(candidate => candidate.DisplayClozeIndex)
                    .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                    .Select(candidate =>
                        $"{candidate.Id}:{candidate.DisplayClozeIndex}:{candidate.IsDisabled}:{candidate.Order}:{FormatPolicy(candidate.PresentationOverride)}:{string.Join(",", candidate.TargetIds.Order(StringComparer.Ordinal))}")),
            string.Join(
                "|",
                slotTargets
                    .OrderBy(target => target.Id, StringComparer.Ordinal)
                    .Select(target =>
                        $"{target.Id}:{target.SlotId}:{target.Answer}:{target.Hint}:{target.IsDisabled}:{string.Join(",", target.TextAnchors.OrderBy(anchor => anchor.Id, StringComparer.Ordinal).Select(anchor => $"{anchor.Id}:{anchor.BlockId}:{anchor.MarkId}:{anchor.RangeId}:{anchor.SelectedTextHash}:{anchor.SelectedTextPreview}"))}:{string.Join(",", target.ImageAnchors.OrderBy(anchor => anchor.Id, StringComparer.Ordinal).Select(anchor => $"{anchor.Id}:{anchor.ImageBlockId}:{anchor.MaskId}"))}")),
            string.Join(
                "|",
                snapshot.Images
                    .OrderBy(image => image.ImageBlockId, StringComparer.Ordinal)
                    .Select(image =>
                        $"{image.ImageBlockId}:{image.ImageRelPath}:{string.Join(",", image.Masks.OrderBy(mask => mask.Id, StringComparer.Ordinal).Select(mask => $"{mask.Id}:{mask.TargetId}:{mask.Shape}:{mask.X}:{mask.Y}:{mask.Width}:{mask.Height}:{mask.Cx}:{mask.Cy}:{mask.Rx}:{mask.Ry}:{FormatPoints(mask.Points)}:{mask.Label}:{mask.IsDisabled}"))}")));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string FormatPoints(IReadOnlyList<NormalizedPoint>? points)
    {
        if (points is null || points.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ";",
            points.Select(point => $"{point.X},{point.Y}"));
    }

    private static string FormatPolicy(CardPresentationPolicy? policy)
    {
        return policy is null
            ? string.Empty
            : string.Join(
                ":",
                policy.Preset,
                policy.TextFrontMode,
                policy.ImageFrontMode,
                policy.BackRevealMode,
                policy.CueCurrentTarget,
                policy.RevealSiblingAnswersOnBack);
    }
}
