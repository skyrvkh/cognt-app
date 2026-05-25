using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private const string Schema = "cognt-flashcard-card";
    private const int Version = 2;
    internal const string BasicCardType = "basic";
    internal const string ClozeCardType = "cloze";
    internal const string MultimodalClozeCardType = "multimodal-cloze";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    internal static IReadOnlyList<int> GetClozeIndexes(string sourceContent)
    {
        string plainText = BlockSuiteDocumentText.ToPlainText(sourceContent);
        return FlashcardClozeText.GetIndexes(plainText);
    }

    internal static string ReplaceEditableContent(string existingContent, string updatedContent)
    {
        if (!TryReadDocument(existingContent, out FlashcardCardDocument? document))
        {
            return updatedContent;
        }

        return string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase)
            ? SerializeDocument(document with { ContextContent = updatedContent })
            : SerializeDocument(document with { FrontContent = updatedContent });
    }

    internal static bool TryUpdateEditableContent(
        string existingContent,
        string frontContent,
        string? backContent,
        IReadOnlyList<string> tags,
        out string updatedContent,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(frontContent);
        ArgumentNullException.ThrowIfNull(tags);

        updatedContent = existingContent;
        error = string.Empty;

        if (!TryReadDocument(existingContent, out FlashcardCardDocument? document))
        {
            error = "Legacy card content is not editable in this dialog.";
            return false;
        }

        string[] normalizedTags = NormalizeTags(tags);
        if (string.Equals(document.CardType, BasicCardType, StringComparison.OrdinalIgnoreCase))
        {
            updatedContent = SerializeDocument(document with
            {
                FrontContent = frontContent,
                BackContent = string.IsNullOrWhiteSpace(backContent) ? frontContent : backContent,
                Tags = normalizedTags,
            });
            return true;
        }

        if (string.Equals(document.CardType, ClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            updatedContent = SerializeDocument(document with
            {
                FrontContent = frontContent,
                Tags = normalizedTags,
            });
            return true;
        }

        if (string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            error = "Multimodal/image occlusion cards must be edited from the note-to-card queue.";
            return false;
        }

        error = $"Card type '{document.CardType}' is not editable in this dialog.";
        return false;
    }
}
