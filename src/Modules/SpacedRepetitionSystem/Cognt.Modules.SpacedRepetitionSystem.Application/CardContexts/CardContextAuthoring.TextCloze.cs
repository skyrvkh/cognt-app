using System.Text.RegularExpressions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    private static readonly Regex ClozeIndexExpression = new(
        @"\{\{c(?<index>\d+)::",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int GetNextDisplayClozeIndex(CardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Slots.Count == 0
            ? 1
            : context.Slots.Max(slot => slot.DisplayClozeIndex) + 1;
    }

    public static int ParseDisplayClozeIndexFromClozeText(string clozeText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clozeText);

        Match match = ClozeIndexExpression.Match(clozeText);
        if (!match.Success || !int.TryParse(match.Groups["index"].Value, out int displayIndex) || displayIndex <= 0)
        {
            throw new ArgumentException("The cloze text does not contain a valid display index.", nameof(clozeText));
        }

        return displayIndex;
    }

    public static TextAnchor CreateTextAnchor(string blockId, string selectedText, string? markId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedText);

        return new TextAnchor(
            NewStableId("text"),
            blockId.Trim(),
            string.IsNullOrWhiteSpace(markId) ? null : markId.Trim(),
            ComputeSelectedTextHash(selectedText),
            null,
            NormalizeInlineText(selectedText));
    }

    public static CardContext AddTextClozeToNewSlot(
        CardContext context,
        string blockId,
        string selectedText,
        int displayClozeIndex)
    {
        ArgumentNullException.ThrowIfNull(context);

        CardSlot slot = CreateNewSlot(displayClozeIndex);
        CardTarget target = CreateCardTarget(slot.Id, selectedText);

        return LinkTextAnchorToTarget(
            AddTargetToExistingSlot(
                AddSlot(context, slot),
                slot.Id,
                target),
            target.Id,
            CreateTextAnchor(blockId, selectedText));
    }

    public static CardContext AddTextClozeToExistingSlot(
        CardContext context,
        string slotId,
        string blockId,
        string selectedText,
        string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        CardTarget target = CreateCardTarget(slotId, selectedText, hint);
        return LinkTextAnchorToTarget(
            AddTargetToExistingSlot(context, slotId, target),
            target.Id,
            CreateTextAnchor(blockId, selectedText));
    }
}
