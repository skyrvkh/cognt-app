using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static class CardContextDraftDocumentCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public static CardContextDraftNoteDocument Read(string? content, string sourceRelPath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return CardContextDraftNoteDocument.Empty(sourceRelPath);
        }

        try
        {
            DraftDocumentEnvelope? document = JsonSerializer.Deserialize<DraftDocumentEnvelope>(content, JsonOptions);
            if (document is null
                || string.IsNullOrWhiteSpace(document.Schema)
                || (!string.Equals(document.Schema, CardContextDraftNoteDocument.CurrentSchema, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, CardContextDraftNoteDocument.LegacySchema, StringComparison.Ordinal)))
            {
                return CardContextDraftNoteDocument.Empty(sourceRelPath);
            }

            IReadOnlyList<CardContext> contexts = document.Contexts
                ?? document.Prompts
                ?? [];

            return new CardContextDraftNoteDocument(
                CardContextDraftNoteDocument.CurrentSchema,
                CardContextDraftNoteDocument.CurrentVersion,
                CardContextDraftNoteDocument.NormalizeRelPath(sourceRelPath),
                contexts);
        }
        catch (JsonException)
        {
            return CardContextDraftNoteDocument.Empty(sourceRelPath);
        }
    }

    public static string Write(CardContextDraftNoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        DraftDocumentEnvelope normalized = new(
            CardContextDraftNoteDocument.CurrentSchema,
            CardContextDraftNoteDocument.CurrentVersion,
            CardContextDraftNoteDocument.NormalizeRelPath(document.SourceRelPath),
            document.Contexts ?? [],
            null);

        return JsonSerializer.Serialize(normalized, JsonOptions) + Environment.NewLine;
    }

    private sealed record DraftDocumentEnvelope(
        string? Schema,
        int Version,
        string? SourceRelPath,
        IReadOnlyList<CardContext>? Contexts,
        IReadOnlyList<CardContext>? Prompts);
}
