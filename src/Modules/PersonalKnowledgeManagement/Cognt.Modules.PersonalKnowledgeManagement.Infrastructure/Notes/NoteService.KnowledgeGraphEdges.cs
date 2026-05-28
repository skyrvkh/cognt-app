using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class KnowledgeGraphBuilder
    {
        private static KnowledgeGraphEdgeReadModel[] CreateGraphEdges(
            IReadOnlyDictionary<string, KnowledgeGraphEdgeAccumulator> edgesByKey,
            IReadOnlySet<string> included,
            IReadOnlyDictionary<string, int> depths,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
            out Dictionary<string, int> incomingCounts,
            out Dictionary<string, int> outgoingCounts)
        {
            List<KnowledgeGraphEdgeAccumulator> includedEdges = new(edgesByKey.Count);
            foreach (KnowledgeGraphEdgeAccumulator edge in edgesByKey.Values)
            {
                if (included.Contains(edge.SourceDocumentId) && included.Contains(edge.TargetDocumentId))
                {
                    includedEdges.Add(edge);
                }
            }

            includedEdges.Sort((left, right) => CompareGraphEdges(left, right, depths, notesByDocumentId));

            incomingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            outgoingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            KnowledgeGraphEdgeReadModel[] edges = new KnowledgeGraphEdgeReadModel[includedEdges.Count];
            for (int index = 0; index < edges.Length; index++)
            {
                KnowledgeGraphEdgeAccumulator edge = includedEdges[index];
                IncrementCount(incomingCounts, edge.TargetDocumentId);
                IncrementCount(outgoingCounts, edge.SourceDocumentId);
                edges[index] = edge.ToReadModel();
            }

            return edges;
        }

        private static int CompareGraphEdges(
            KnowledgeGraphEdgeAccumulator left,
            KnowledgeGraphEdgeAccumulator right,
            IReadOnlyDictionary<string, int> depths,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId)
        {
            int depthComparison = depths[left.SourceDocumentId].CompareTo(depths[right.SourceDocumentId]);
            if (depthComparison != 0)
            {
                return depthComparison;
            }

            int sourceTitleComparison = string.Compare(
                notesByDocumentId[left.SourceDocumentId].Title,
                notesByDocumentId[right.SourceDocumentId].Title,
                StringComparison.OrdinalIgnoreCase);
            if (sourceTitleComparison != 0)
            {
                return sourceTitleComparison;
            }

            int targetTitleComparison = string.Compare(
                notesByDocumentId[left.TargetDocumentId].Title,
                notesByDocumentId[right.TargetDocumentId].Title,
                StringComparison.OrdinalIgnoreCase);
            if (targetTitleComparison != 0)
            {
                return targetTitleComparison;
            }

            int sourceComparison = string.Compare(left.SourceDocumentId, right.SourceDocumentId, StringComparison.OrdinalIgnoreCase);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            int targetComparison = string.Compare(left.TargetDocumentId, right.TargetDocumentId, StringComparison.OrdinalIgnoreCase);
            return targetComparison != 0
                ? targetComparison
                : left.SequenceIndex.CompareTo(right.SequenceIndex);
        }

        private static void IncrementCount(Dictionary<string, int> counts, string documentId)
        {
            counts.TryGetValue(documentId, out int count);
            counts[documentId] = count + 1;
        }

        private sealed class KnowledgeGraphEdgeAccumulator
        {
            internal KnowledgeGraphEdgeAccumulator(string sourceDocumentId, string targetDocumentId, int sequenceIndex)
            {
                SourceDocumentId = sourceDocumentId;
                TargetDocumentId = targetDocumentId;
                SequenceIndex = sequenceIndex;
            }

            internal string SourceDocumentId { get; }

            internal string TargetDocumentId { get; }

            internal int SequenceIndex { get; }

            private int MentionCount { get; set; }

            private long? UpdatedAt { get; set; }

            internal void Add(DocumentMentionRow mention)
            {
                MentionCount++;
                UpdatedAt = UpdatedAt.HasValue
                    ? Math.Max(UpdatedAt.Value, mention.UpdatedAt)
                    : mention.UpdatedAt;
            }

            internal KnowledgeGraphEdgeReadModel ToReadModel()
            {
                return new KnowledgeGraphEdgeReadModel(
                    SourceDocumentId,
                    TargetDocumentId,
                    MentionCount,
                    UpdatedAt);
            }
        }
    }
}
