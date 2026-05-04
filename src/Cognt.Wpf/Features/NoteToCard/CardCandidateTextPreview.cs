using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard;

internal static class CardCandidateTextPreview
{
    private const string HiddenClozeText = "[[...]]";
    private const string CurrentHiddenClozeText = "[[current ...]]";
    private const string ExplicitClozeIndexGroupName = "explicit";
    private const string ExplicitClozeTextGroupName = "explicitText";
    private const string SimpleClozeTextGroupName = "simpleText";

    private static readonly Regex ClozeRegex = new(
        @"\{\{(?:(?:c(?<explicit>\d+)::?)(?<explicitText>.*?)(?:::.*?)?|(?<simpleText>[^{}]+?))\}\}",
        RegexOptions.Compiled);

    internal static string CreateFrontText(
        string contextContent,
        IReadOnlyList<CardSlot> slots,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        return Render(
            ExtractPlainText(contextContent),
            slots,
            currentSlotId,
            presentation,
            revealAnswers: false);
    }

    internal static string CreateBackText(
        string contextContent,
        IReadOnlyList<CardSlot> slots,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        return Render(
            ExtractPlainText(contextContent),
            slots,
            currentSlotId,
            presentation,
            revealAnswers: true);
    }

    private static string Render(
        string text,
        IReadOnlyList<CardSlot> slots,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool revealAnswers)
    {
        MatchCollection matches = ClozeRegex.Matches(text);
        if (matches.Count == 0)
        {
            return string.IsNullOrWhiteSpace(text)
                ? "No previewable text was found in this card context."
                : text;
        }

        Dictionary<int, CardSlot> slotsByDisplayIndex = slots
            .Where(slot => slot.DisplayClozeIndex > 0)
            .GroupBy(slot => slot.DisplayClozeIndex)
            .Select(grouping => grouping.First())
            .ToDictionary(slot => slot.DisplayClozeIndex, slot => slot);

        StringBuilder builder = new();
        int position = 0;
        int nextSimpleIndex = 1;
        foreach (Match match in matches)
        {
            builder.Append(text, position, match.Index - position);

            int displayIndex = match.Groups[ExplicitClozeIndexGroupName].Success
                ? int.Parse(match.Groups[ExplicitClozeIndexGroupName].Value)
                : nextSimpleIndex++;
            string answer = match.Groups[ExplicitClozeTextGroupName].Success
                ? match.Groups[ExplicitClozeTextGroupName].Value
                : match.Groups[SimpleClozeTextGroupName].Value;
            slotsByDisplayIndex.TryGetValue(displayIndex, out CardSlot? slot);

            if (revealAnswers)
            {
                builder.Append(RenderBackCloze(answer, slot?.Id, currentSlotId, presentation));
            }
            else if (slot is null)
            {
                builder.Append(answer);
            }
            else
            {
                builder.Append(RenderFrontCloze(answer, slot.Id, currentSlotId, presentation));
            }

            position = match.Index + match.Length;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    private static string RenderFrontCloze(
        string answer,
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        bool isCurrentSlot = string.Equals(slotId, currentSlotId, StringComparison.Ordinal);
        return presentation.TextFrontMode switch
        {
            TextClozeFrontMode.None or TextClozeFrontMode.ShowAll => answer,
            TextClozeFrontMode.HideCurrentShowSiblings when isCurrentSlot => CurrentHiddenClozeText,
            TextClozeFrontMode.HideCurrentShowSiblings => answer,
            TextClozeFrontMode.HideAllCueCurrent when isCurrentSlot => CurrentHiddenClozeText,
            TextClozeFrontMode.HideAllCueCurrent => HiddenClozeText,
            TextClozeFrontMode.HideAllSameStyle => HiddenClozeText,
            _ => HiddenClozeText,
        };
    }

    private static string RenderBackCloze(
        string answer,
        string? slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        if (string.IsNullOrWhiteSpace(slotId)
            || ShouldRevealSlotOnBack(slotId, currentSlotId, presentation)
            || presentation.TextFrontMode is TextClozeFrontMode.HideCurrentShowSiblings
                or TextClozeFrontMode.None
                or TextClozeFrontMode.ShowAll)
        {
            return answer;
        }

        return HiddenClozeText;
    }

    private static bool ShouldRevealSlotOnBack(
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        if (presentation.RevealSiblingAnswersOnBack
            || presentation.BackRevealMode is CardBackRevealMode.RevealAllTargets or CardBackRevealMode.RevealAllSlots)
        {
            return true;
        }

        return string.Equals(slotId, currentSlotId, StringComparison.Ordinal);
    }

    private static string ExtractPlainText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            StringBuilder builder = new();
            AppendTextValues(document.RootElement, builder);
            return builder.ToString().Trim();
        }
        catch (JsonException)
        {
            return content.Trim();
        }
    }

    private static void AppendTextValues(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsPageBlock(element))
                {
                    if (element.TryGetProperty("children", out JsonElement pageChildren))
                    {
                        AppendTextValues(pageChildren, builder);
                    }

                    return;
                }

                if (TryAppendBlockSuiteText(element, builder))
                {
                    return;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AppendTextValues(property.Value, builder);
                }

                return;
            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                {
                    AppendTextValues(child, builder);
                }

                return;
        }
    }

    private static bool IsPageBlock(JsonElement element)
    {
        return element.TryGetProperty("flavour", out JsonElement flavour)
            && flavour.ValueKind == JsonValueKind.String
            && string.Equals(flavour.GetString(), "affine:page", StringComparison.Ordinal);
    }

    private static bool TryAppendBlockSuiteText(JsonElement element, StringBuilder builder)
    {
        if (!element.TryGetProperty("$blocksuite:internal:text$", out JsonElement marker)
            || marker.ValueKind != JsonValueKind.True
            || !element.TryGetProperty("delta", out JsonElement delta)
            || delta.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement operation in delta.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object
                || !operation.TryGetProperty("insert", out JsonElement insert)
                || insert.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(insert.GetString());
        }

        return true;
    }
}
