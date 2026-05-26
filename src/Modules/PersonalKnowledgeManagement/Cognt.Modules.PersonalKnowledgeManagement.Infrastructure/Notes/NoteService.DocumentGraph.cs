using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    KnowledgeGraphReadModel INoteService.ReadDocumentGraph(
        string collectionId,
        string currentRelativePath,
        string currentContent,
        KnowledgeGraphSettingsReadModel settings)
    {
        KnowledgeGraphSettingsReadModel normalizedSettings = (settings ?? KnowledgeGraphSettingsReadModel.Default).Normalize();
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedCurrentRelativePath = NormalizeRelativePath(currentRelativePath);
        _ = ResolveExistingRelativeNotePathInRoot(root, normalizedCurrentRelativePath);

        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        Dictionary<string, DocumentLinkNote> notesByPath = LoadDocumentLinkNotesByRelativePath(root, repository);
        if (!notesByPath.TryGetValue(normalizedCurrentRelativePath, out DocumentLinkNote? currentNote))
        {
            return KnowledgeGraphReadModel.Empty(normalizedSettings);
        }

        EnsureDocumentMentionIndexCurrent(
            root,
            repository,
            currentNote,
            currentContent,
            notesByPath);

        Dictionary<string, DocumentLinkNote> notesByDocumentId = CreateNotesByDocumentId(notesByPath);

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
            foreach (DocumentMentionRow mention in repository.LoadResolvedDocumentMentionsByDocument(documentId))
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

        return new KnowledgeGraphReadModel(
            currentNote.DocumentId,
            currentNote.RelativePath,
            normalizedSettings,
            nodes,
            edges,
            depths.Count,
            depths.Count > includedDocumentIds.Length);
    }

    KnowledgeGraphSettingsReadModel INoteService.LoadGraphViewSettings(string collectionId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        return repository.LoadGraphViewSettings().Normalize();
    }

    void INoteService.SaveGraphViewSettings(
        string collectionId,
        KnowledgeGraphSettingsReadModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        repository.SaveGraphViewSettings(settings.Normalize());
    }

    private static bool TryNormalizeGraphMention(
        DocumentMentionRow mention,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
        out string sourceDocumentId,
        out string targetDocumentId)
    {
        sourceDocumentId = string.Empty;
        targetDocumentId = string.Empty;

        if (!string.Equals(mention.Status, ResolvedMentionStatus, StringComparison.OrdinalIgnoreCase)
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

    private static Dictionary<string, DocumentLinkNote> CreateNotesByDocumentId(
        Dictionary<string, DocumentLinkNote> notesByPath)
    {
        Dictionary<string, DocumentLinkNote> notesByDocumentId = new(notesByPath.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DocumentLinkNote note in notesByPath.Values)
        {
            notesByDocumentId.Add(note.DocumentId, note);
        }

        return notesByDocumentId;
    }

    private static string[] CreateIncludedDocumentIds(
        Dictionary<string, int> depths,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
        string currentDocumentId,
        int maxNodes)
    {
        string[] documentIds = new string[depths.Count];
        Dictionary<string, int> originalOrderByDocumentId = new(depths.Count, StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (string documentId in depths.Keys)
        {
            documentIds[index] = documentId;
            originalOrderByDocumentId.Add(documentId, index);
            index++;
        }

        Array.Sort(
            documentIds,
            (left, right) => CompareIncludedDocumentIds(
                left,
                right,
                depths,
                notesByDocumentId,
                currentDocumentId,
                originalOrderByDocumentId));

        if (maxNodes >= documentIds.Length)
        {
            return documentIds;
        }

        string[] limitedDocumentIds = new string[maxNodes];
        Array.Copy(documentIds, limitedDocumentIds, limitedDocumentIds.Length);
        return limitedDocumentIds;
    }

    private static int CompareIncludedDocumentIds(
        string left,
        string right,
        IReadOnlyDictionary<string, int> depths,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
        string currentDocumentId,
        IReadOnlyDictionary<string, int> originalOrderByDocumentId)
    {
        int currentComparison = IsCurrentDocument(left, currentDocumentId).CompareTo(IsCurrentDocument(right, currentDocumentId));
        if (currentComparison != 0)
        {
            return currentComparison;
        }

        int depthComparison = depths[left].CompareTo(depths[right]);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        DocumentLinkNote leftNote = notesByDocumentId[left];
        DocumentLinkNote rightNote = notesByDocumentId[right];
        int updatedComparison = rightNote.UpdatedAt.CompareTo(leftNote.UpdatedAt);
        if (updatedComparison != 0)
        {
            return updatedComparison;
        }

        int titleComparison = string.Compare(leftNote.Title, rightNote.Title, StringComparison.OrdinalIgnoreCase);
        if (titleComparison != 0)
        {
            return titleComparison;
        }

        int pathComparison = string.Compare(leftNote.RelativePath, rightNote.RelativePath, StringComparison.OrdinalIgnoreCase);
        return pathComparison != 0
            ? pathComparison
            : originalOrderByDocumentId[left].CompareTo(originalOrderByDocumentId[right]);
    }

    private static int IsCurrentDocument(string documentId, string currentDocumentId)
    {
        return string.Equals(documentId, currentDocumentId, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

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

    private static KnowledgeGraphNodeReadModel[] CreateGraphNodes(
        IReadOnlyList<string> includedDocumentIds,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByDocumentId,
        IReadOnlyDictionary<string, int> depths,
        string currentDocumentId,
        IReadOnlyDictionary<string, int> incomingCounts,
        IReadOnlyDictionary<string, int> outgoingCounts)
    {
        KnowledgeGraphNodeReadModel[] nodes = new KnowledgeGraphNodeReadModel[includedDocumentIds.Count];
        for (int index = 0; index < nodes.Length; index++)
        {
            string documentId = includedDocumentIds[index];
            DocumentLinkNote note = notesByDocumentId[documentId];
            nodes[index] = new KnowledgeGraphNodeReadModel(
                note.DocumentId,
                note.RelativePath,
                note.Title,
                depths[documentId],
                string.Equals(note.DocumentId, currentDocumentId, StringComparison.OrdinalIgnoreCase),
                note.UpdatedAt,
                incomingCounts.GetValueOrDefault(note.DocumentId),
                outgoingCounts.GetValueOrDefault(note.DocumentId));
        }

        return nodes;
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
