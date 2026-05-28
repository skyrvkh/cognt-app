using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class KnowledgeGraphBuilder
    {
        private const string ResolvedMentionStatusValue = "resolved";

        internal static KnowledgeGraphReadModel Build(
            DocumentLinkNote currentNote,
            KnowledgeGraphSettingsReadModel normalizedSettings,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
            Func<string, IReadOnlyList<DocumentMentionRow>> loadMentionsByDocument)
        {
            ArgumentNullException.ThrowIfNull(currentNote);
            ArgumentNullException.ThrowIfNull(normalizedSettings);
            ArgumentNullException.ThrowIfNull(notesByDocumentId);
            ArgumentNullException.ThrowIfNull(loadMentionsByDocument);


            Dictionary<string, int> depths = new(StringComparer.OrdinalIgnoreCase)
            {
                [currentNote.DocumentId] = 0,
            };
            Queue<string> queue = new();
            queue.Enqueue(currentNote.DocumentId);
            Dictionary<string, KnowledgeGraphEdgeAccumulator> edgesByKey = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenMentionIds = new(StringComparer.OrdinalIgnoreCase);

            while (queue.TryDequeue(out string? documentId))
            {
                if (!notesByDocumentId.ContainsKey(documentId))
                {
                    continue;
                }

                int depth = depths[documentId];
                foreach (DocumentMentionRow mention in loadMentionsByDocument(documentId))
                {
                    if (!TryNormalizeGraphMention(
                        mention,
                        notesByDocumentId,
                        out string? sourceDocumentId,
                        out string? targetDocumentId)
                        || !seenMentionIds.Add(mention.Id))
                    {
                        continue;
                    }

                    string edgeKey = CreateGraphEdgeKey(sourceDocumentId, targetDocumentId);
                    if (!edgesByKey.TryGetValue(edgeKey, out KnowledgeGraphEdgeAccumulator? edge))
                    {
                        edge = new KnowledgeGraphEdgeAccumulator(sourceDocumentId, targetDocumentId, edgesByKey.Count);
                        edgesByKey[edgeKey] = edge;
                    }

                    edge.Add(mention);

                    if (depth >= normalizedSettings.NeighborDepth)
                    {
                        continue;
                    }

                    string neighborDocumentId = string.Equals(
                        documentId,
                        sourceDocumentId,
                        StringComparison.OrdinalIgnoreCase)
                            ? targetDocumentId
                            : sourceDocumentId;
                    int neighborDepth = depth + 1;

                    if (!depths.TryGetValue(neighborDocumentId, out int existingDepth)
                        || neighborDepth < existingDepth)
                    {
                        depths[neighborDocumentId] = neighborDepth;
                        queue.Enqueue(neighborDocumentId);
                    }
                }
            }

            string[] includedDocumentIds = CreateIncludedDocumentIds(
                depths,
                notesByDocumentId,
                currentNote.DocumentId,
                normalizedSettings.MaxNodes);

            HashSet<string> included = new(includedDocumentIds, StringComparer.OrdinalIgnoreCase);
            KnowledgeGraphEdgeReadModel[] edges = CreateGraphEdges(
                edgesByKey,
                included,
                depths,
                notesByDocumentId,
                out Dictionary<string, int> incomingCounts,
                out Dictionary<string, int> outgoingCounts);
            KnowledgeGraphNodeReadModel[] nodes = CreateGraphNodes(
                includedDocumentIds,
                notesByDocumentId,
                depths,
                currentNote.DocumentId,
                incomingCounts,
                outgoingCounts);

            KnowledgeGraphReadModel graph = new(
                currentNote.DocumentId,
                currentNote.RelativePath,
                normalizedSettings,
                nodes,
                edges,
                depths.Count,
                depths.Count > includedDocumentIds.Length);
            return graph;
        }

    }
}
