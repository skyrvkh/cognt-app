using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class KnowledgeGraphBuilder
    {
        private static bool TryNormalizeGraphMention(
            DocumentMentionRow mention,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
            out string sourceDocumentId,
            out string targetDocumentId)
        {
            sourceDocumentId = string.Empty;
            targetDocumentId = string.Empty;

            if (!string.Equals(mention.Status, ResolvedMentionStatusValue, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mention.TargetDocumentId)
                || string.Equals(mention.SourceDocumentId, mention.TargetDocumentId, StringComparison.OrdinalIgnoreCase)
                || !notesByDocumentId.ContainsKey(mention.SourceDocumentId)
                || !notesByDocumentId.ContainsKey(mention.TargetDocumentId))
            {
                return false;
            }

            sourceDocumentId = mention.SourceDocumentId;
            targetDocumentId = mention.TargetDocumentId;
            return true;
        }

        private static string CreateGraphEdgeKey(string sourceDocumentId, string targetDocumentId)
        {
            return string.Join('|', sourceDocumentId.ToUpperInvariant(), targetDocumentId.ToUpperInvariant());
        }

        private static string[] CreateIncludedDocumentIds(
            Dictionary<string, int> depths,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
            string currentDocumentId,
            int maxNodes)
        {
            return depths.Keys
                .Select((documentId, index) => (
                    DocumentId: documentId,
                    Depth: depths[documentId],
                    Note: notesByDocumentId[documentId],
                    Index: index))
                .OrderBy(item => string.Equals(item.DocumentId, currentDocumentId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(static item => item.Depth)
                .ThenByDescending(static item => item.Note.UpdatedAt)
                .ThenBy(static item => item.Note.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Note.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Index)
                .Take(maxNodes)
                .Select(static item => item.DocumentId)
                .ToArray();
        }
    }
}
