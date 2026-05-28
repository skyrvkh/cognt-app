using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class KnowledgeGraphBuilder
    {
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
    }
}
