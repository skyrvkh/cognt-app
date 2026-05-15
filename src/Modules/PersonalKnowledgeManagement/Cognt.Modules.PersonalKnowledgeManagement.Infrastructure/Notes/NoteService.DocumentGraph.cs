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

        Dictionary<string, DocumentLinkNote> notesByDocumentId = notesByPath.Values
            .ToDictionary(note => note.DocumentId, note => note, StringComparer.OrdinalIgnoreCase);

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
                    edge = new KnowledgeGraphEdgeAccumulator(sourceDocumentId, targetDocumentId);
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

        string[] includedDocumentIds = depths.Keys
            .OrderBy(documentId => string.Equals(documentId, currentNote.DocumentId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(documentId => depths[documentId])
            .ThenByDescending(documentId => notesByDocumentId[documentId].UpdatedAt)
            .ThenBy(documentId => notesByDocumentId[documentId].Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(documentId => notesByDocumentId[documentId].RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(normalizedSettings.MaxNodes)
            .ToArray();

        HashSet<string> included = new(includedDocumentIds, StringComparer.OrdinalIgnoreCase);
        KnowledgeGraphEdgeReadModel[] edges = edgesByKey.Values
            .Where(edge => included.Contains(edge.SourceDocumentId) && included.Contains(edge.TargetDocumentId))
            .OrderBy(edge => depths[edge.SourceDocumentId])
            .ThenBy(edge => notesByDocumentId[edge.SourceDocumentId].Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => notesByDocumentId[edge.TargetDocumentId].Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.SourceDocumentId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetDocumentId, StringComparer.OrdinalIgnoreCase)
            .Select(edge => edge.ToReadModel())
            .ToArray();

        Dictionary<string, int> incomingCounts = edges
            .GroupBy(edge => edge.TargetDocumentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> outgoingCounts = edges
            .GroupBy(edge => edge.SourceDocumentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        KnowledgeGraphNodeReadModel[] nodes = includedDocumentIds
            .Select(documentId =>
            {
                DocumentLinkNote note = notesByDocumentId[documentId];
                return new KnowledgeGraphNodeReadModel(
                    note.DocumentId,
                    note.RelativePath,
                    note.Title,
                    depths[documentId],
                    string.Equals(note.DocumentId, currentNote.DocumentId, StringComparison.OrdinalIgnoreCase),
                    note.UpdatedAt,
                    incomingCounts.GetValueOrDefault(note.DocumentId),
                    outgoingCounts.GetValueOrDefault(note.DocumentId));
            })
            .ToArray();

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

    private sealed class KnowledgeGraphEdgeAccumulator
    {
        internal KnowledgeGraphEdgeAccumulator(string sourceDocumentId, string targetDocumentId)
        {
            SourceDocumentId = sourceDocumentId;
            TargetDocumentId = targetDocumentId;
        }

        internal string SourceDocumentId { get; }

        internal string TargetDocumentId { get; }

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
