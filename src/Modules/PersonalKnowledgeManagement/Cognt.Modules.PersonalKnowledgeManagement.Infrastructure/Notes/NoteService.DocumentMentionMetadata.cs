using System.Security.Cryptography;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed class DocumentMentionContext
    {
        internal DocumentMentionContext(
            string previewText,
            string? contextBefore,
            string? contextAfter)
        {
            PreviewText = previewText;
            ContextBefore = contextBefore;
            ContextAfter = contextAfter;
        }

        internal string PreviewText { get; }

        internal string? ContextBefore { get; }

        internal string? ContextAfter { get; }
    }

    private static DocumentMentionContext CreateMentionContext(string text, int start, int end)
    {
        const int previewRadius = 150;
        const int contextRadius = 80;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentMentionContext(string.Empty, null, null);
        }

        int safeStart = Math.Clamp(start, 0, text.Length);
        int safeEnd = Math.Clamp(end, safeStart, text.Length);
        int previewStart = Math.Max(0, safeStart - previewRadius);
        int previewEnd = Math.Min(text.Length, safeEnd + previewRadius);
        int beforeStart = Math.Max(0, safeStart - contextRadius);
        int afterEnd = Math.Min(text.Length, safeEnd + contextRadius);
        string previewPrefix = previewStart > 0 ? "... " : string.Empty;
        string previewSuffix = previewEnd < text.Length ? " ..." : string.Empty;
        string preview = $"{previewPrefix}{NormalizePreviewText(text[previewStart..previewEnd])}{previewSuffix}";
        string? before = safeStart > beforeStart ? NormalizePreviewText(text[beforeStart..safeStart]) : null;
        string? after = afterEnd > safeEnd ? NormalizePreviewText(text[safeEnd..afterEnd]) : null;
        return new DocumentMentionContext(preview, before, after);
    }

    private static string NormalizeMentionText(string value)
    {
        return SearchableDocumentText.NormalizeWhitespace(value).ToLowerInvariant();
    }

    private static string NormalizePreviewText(string value)
    {
        return SearchableDocumentText.NormalizeWhitespace(value);
    }

    private static string CreateDocumentMentionId(
        string sourceDocumentId,
        string sourceBlockId,
        string? targetDocumentId,
        int startOffset,
        int endOffset,
        string mentionText,
        string mentionType)
    {
        string input = string.Join(
            '|',
            sourceDocumentId,
            sourceBlockId,
            targetDocumentId ?? string.Empty,
            startOffset,
            endOffset,
            mentionType,
            mentionText);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string CreateShortHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    }
}
