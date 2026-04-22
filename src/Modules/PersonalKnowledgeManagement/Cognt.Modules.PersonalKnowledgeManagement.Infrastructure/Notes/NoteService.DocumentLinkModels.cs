namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed class DocumentLinkNote
    {
        internal DocumentLinkNote(
            string documentId,
            string relativePath,
            string title,
            long updatedAt)
        {
            DocumentId = documentId;
            RelativePath = relativePath;
            Title = title;
            UpdatedAt = updatedAt;
        }

        internal string DocumentId { get; }

        internal string RelativePath { get; }

        internal string Title { get; }

        internal long UpdatedAt { get; }
    }

    private sealed class DocumentLinkTitleIndex
    {
        internal DocumentLinkTitleIndex(
            IReadOnlyDictionary<string, DocumentLinkNote> unambiguousTitles,
            IReadOnlyDictionary<string, IReadOnlyList<DocumentLinkNote>> ambiguousTitles)
        {
            UnambiguousTitles = unambiguousTitles;
            AmbiguousTitles = ambiguousTitles;
        }

        internal IReadOnlyDictionary<string, DocumentLinkNote> UnambiguousTitles { get; }

        internal IReadOnlyDictionary<string, IReadOnlyList<DocumentLinkNote>> AmbiguousTitles { get; }
    }
}
