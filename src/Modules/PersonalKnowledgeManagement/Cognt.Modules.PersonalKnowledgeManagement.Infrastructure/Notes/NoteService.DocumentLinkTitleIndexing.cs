using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static DocumentLinkTitleIndex BuildDocumentLinkTitleIndex(
        IEnumerable<DocumentLinkNote> notes)
    {
        IEnumerable<IGrouping<string, DocumentLinkNote>> notesByTitle = notes
            .Where(note => note.Title.Trim().Length >= 3)
            .GroupBy(note => NormalizeDocumentTitleLookupKey(note.Title), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, DocumentLinkNote> unambiguousTitles = [];
        Dictionary<string, IReadOnlyList<DocumentLinkNote>> ambiguousTitles = [];
        foreach (IGrouping<string, DocumentLinkNote> group in notesByTitle)
        {
            DocumentLinkNote[] candidates = group.ToArray();
            if (candidates.Length == 1)
            {
                unambiguousTitles[group.Key] = candidates[0];
                continue;
            }

            ambiguousTitles[group.Key] = candidates;
        }

        return new DocumentLinkTitleIndex(unambiguousTitles, ambiguousTitles);
    }

    private static string NormalizeDocumentTitleLookupKey(string title)
    {
        return SearchableDocumentText.NormalizeWhitespace(title);
    }
}
