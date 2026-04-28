using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static List<KnowledgeDocumentLinkReadModel> MaterializeDocumentLinkReadModels(
        string direction,
        IEnumerable<DocumentMentionRow> mentions,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId)
    {
        List<KnowledgeDocumentLinkReadModel> links = [];
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (DocumentMentionRow mention in mentions)
        {
            if (!string.Equals(mention.Status, ResolvedMentionStatus, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mention.TargetDocumentId)
                || !notesByDocumentId.TryGetValue(mention.SourceDocumentId, out DocumentLinkNote? source)
                || !notesByDocumentId.TryGetValue(mention.TargetDocumentId, out DocumentLinkNote? target))
            {
                continue;
            }

            bool incoming = string.Equals(direction, "incoming", StringComparison.OrdinalIgnoreCase);
            DocumentLinkNote display = incoming ? source : target;
            string openRelativePath = incoming ? source.RelativePath : target.RelativePath;
            string linkKey = CreateDocumentLinkDeduplicationKey(
                mention.Id,
                direction,
                source.RelativePath,
                target.RelativePath,
                mention.SourceBlockId,
                mention.StartOffset,
                mention.EndOffset);
            if (!seenKeys.Add(linkKey))
            {
                continue;
            }

            links.Add(new KnowledgeDocumentLinkReadModel(
                openRelativePath,
                display.Title,
                mention.PreviewText,
                mention.MentionText,
                display.UpdatedAt));
        }

        return links;
    }

    private static string CreateDocumentLinkDeduplicationKey(
        string mentionId,
        string direction,
        string sourceRelativePath,
        string targetRelativePath,
        string sourceBlockId,
        int startOffset,
        int endOffset)
    {
        return string.Join(
            '|',
            mentionId,
            direction,
            sourceRelativePath.ToUpperInvariant(),
            targetRelativePath.ToUpperInvariant(),
            sourceBlockId.ToUpperInvariant(),
            startOffset,
            endOffset);
    }
}
