using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class DocumentMentionBuilder
    {
        internal IReadOnlyList<DocumentMentionRow> MaterializeRows(long now)
        {
            string fullText = Text;
            List<DocumentMentionRow> rows = new(occurrences.Count);
            foreach (DocumentMentionOccurrence occurrence in occurrences)
            {
                DocumentMentionRow row = MaterializeRow(occurrence, fullText, now);
                if (!string.IsNullOrWhiteSpace(row.PreviewText))
                {
                    rows.Add(row);
                }
            }

            return rows.ToArray();
        }

        private DocumentMentionRow MaterializeRow(
            DocumentMentionOccurrence occurrence,
            string fullText,
            long now)
        {
            DocumentMentionContext context = CreateMentionContext(
                fullText,
                occurrence.StartOffset,
                occurrence.EndOffset);
            string normalizedMentionText = NormalizeMentionText(occurrence.MentionText);
            string id = CreateDocumentMentionId(
                source.DocumentId,
                sourceBlockId,
                occurrence.TargetDocumentId,
                occurrence.StartOffset,
                occurrence.EndOffset,
                occurrence.MentionText,
                occurrence.MentionType);

            return new DocumentMentionRow(
                id,
                source.DocumentId,
                sourceBlockId,
                occurrence.TargetDocumentId,
                occurrence.MentionType,
                occurrence.MentionText,
                normalizedMentionText,
                occurrence.StartOffset,
                occurrence.EndOffset,
                context.PreviewText,
                context.ContextBefore,
                context.ContextAfter,
                occurrence.Status,
                now,
                now);
        }
    }
}
