using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string NormalizeSearchText(
        string title,
        string relativePath,
        string pathText,
        string plainText,
        IReadOnlyList<string> tags)
    {
        return SearchableDocumentText.NormalizeWhitespace(string.Join(
            ' ',
            new[]
            {
                title,
                relativePath,
                pathText,
                plainText,
                string.Join(' ', tags)
            }));
    }

    private sealed class NoteCatalogEntry
    {
        internal NoteCatalogEntry(KnowledgeNoteListItemReadModel item, string searchText)
        {
            Item = item;
            SearchText = searchText;
        }

        internal KnowledgeNoteListItemReadModel Item { get; }

        internal string SearchText { get; }
    }
}
