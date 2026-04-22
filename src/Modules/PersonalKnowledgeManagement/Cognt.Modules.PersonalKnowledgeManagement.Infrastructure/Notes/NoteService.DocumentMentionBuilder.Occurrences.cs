using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed class DocumentMentionOccurrence
    {
        internal DocumentMentionOccurrence(
            string? targetDocumentId,
            string mentionType,
            string mentionText,
            int startOffset,
            int endOffset,
            string status)
        {
            TargetDocumentId = targetDocumentId;
            MentionType = mentionType;
            MentionText = mentionText;
            StartOffset = startOffset;
            EndOffset = endOffset;
            Status = status;
        }

        internal string? TargetDocumentId { get; }

        internal string MentionType { get; }

        internal string MentionText { get; }

        internal int StartOffset { get; }

        internal int EndOffset { get; }

        internal string Status { get; }
    }

    private sealed partial class DocumentMentionBuilder
    {
        private void AddMentionOccurrence(
            string? targetDocumentId,
            string mentionType,
            string matchedText,
            int start,
            int length,
            string status,
            bool skipWhenOverlapping)
        {
            int safeStart = Math.Clamp(start, 0, text.Length);
            int safeLength = Math.Clamp(length, 0, text.Length - safeStart);
            if (safeLength <= 0 || string.IsNullOrWhiteSpace(matchedText))
            {
                return;
            }

            int end = safeStart + safeLength;
            if (skipWhenOverlapping && HasOverlappingMentionRange(safeStart, end))
            {
                return;
            }

            occurrences.Add(new DocumentMentionOccurrence(
                targetDocumentId,
                mentionType,
                SearchableDocumentText.NormalizeWhitespace(matchedText),
                safeStart,
                end,
                status));
        }

        private bool HasOverlappingMentionRange(int start, int end)
        {
            return occurrences.Any(occurrence =>
                start < occurrence.EndOffset && end > occurrence.StartOffset);
        }
    }
}
