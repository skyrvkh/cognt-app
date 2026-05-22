using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static string SerializeDocument(FlashcardCardDocument document)
    {
        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    private static bool TryReadDocument(
        string content,
        [NotNullWhen(true)] out FlashcardCardDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            document = JsonSerializer.Deserialize<FlashcardCardDocument>(content, JsonOptions);
            return document is not null && IsSupportedDocument(document);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string[] NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(NormalizeTagDisplayName)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }

    private sealed record FlashcardCardDocument(
        string? Schema,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kind,
        int Version,
        string CardType,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FrontContent,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BackContent,
        int? ClozeIndex,
        IReadOnlyList<string>? Tags,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FlashcardCardSourceDocument? Source,
        [property: JsonPropertyName("frontPolicy")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FlashcardFrontPolicyDocument? FrontPolicy,
        [property: JsonPropertyName("presentation")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CardPresentationPolicy? Presentation,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContextContent,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CardSlot>? Slots,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CardSlot>? Groups,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CardTarget>? Targets,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CardContextImageSurface>? Images,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CurrentSlotId);

    private sealed record FlashcardCardSourceDocument(
        string Kind,
        string RelPath,
        string ContextId,
        string SlotId,
        IReadOnlyList<string> TargetIds,
        string SnapshotHash);

    private sealed record FlashcardFrontPolicyDocument(
        CardCueMode Text,
        CardCueMode Image);

    private static bool IsSupportedDocument(FlashcardCardDocument document)
    {
        string? schema = document.Schema ?? document.Kind;
        if (!string.Equals(schema, Schema, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.CardType))
        {
            return false;
        }

        if (string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(document.ContextContent)
                && (document.Slots is not null || document.Groups is not null)
                && document.Targets is not null
                && !string.IsNullOrWhiteSpace(document.CurrentSlotId);
        }

        return !string.IsNullOrWhiteSpace(document.FrontContent);
    }
}
