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

        if (string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeDocument((document with { ContextContent = updatedContent })
                .WithSynchronizedTextClozeSlots(updatedContent));
        }

        if (string.Equals(document.CardType, BasicCardType, StringComparison.OrdinalIgnoreCase)
            && ResolveEditedClozeIndex(updatedContent, null) is int newClozeIndex)
        {
            return SerializeDocument(document with
            {
                CardType = ClozeCardType,
                FrontContent = updatedContent,
                BackContent = null,
                ClozeIndex = newClozeIndex,
            });
        }

        if (string.Equals(document.CardType, ClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeDocument(document with
            {
                FrontContent = updatedContent,
                ClozeIndex = ResolveEditedClozeIndex(updatedContent, document.ClozeIndex),
            });
        }

        return SerializeDocument(document with { FrontContent = updatedContent });
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
            if (ResolveEditedClozeIndex(frontContent, null) is int newClozeIndex)
            {
                updatedContent = SerializeDocument(document with
                {
                    CardType = ClozeCardType,
                    FrontContent = frontContent,
                    BackContent = null,
                    ClozeIndex = newClozeIndex,
                    Tags = normalizedTags,
                });
                return true;
            }

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
            int? clozeIndex = ResolveEditedClozeIndex(frontContent, document.ClozeIndex);
            updatedContent = SerializeDocument(document with
            {
                FrontContent = frontContent,
                ClozeIndex = clozeIndex,
                Tags = normalizedTags,
            });
            return true;
        }

        if (string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            if (HasImageOcclusion(document))
            {
                error = "Image occlusion cards are not editable in this text editor yet.";
                return false;
            }

            updatedContent = SerializeDocument((document with
            {
                ContextContent = frontContent,
                Tags = normalizedTags,
            }).WithSynchronizedTextClozeSlots(frontContent));
            return true;
        }

        error = $"Card type '{document.CardType}' is not editable in this dialog.";
        return false;
    }

    private static bool HasImageOcclusion(FlashcardCardDocument document)
    {
        if (document.Images is null)
        {
            return false;
        }

        foreach (CardContextImageSurface image in document.Images)
        {
            if (image.Masks.Count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
