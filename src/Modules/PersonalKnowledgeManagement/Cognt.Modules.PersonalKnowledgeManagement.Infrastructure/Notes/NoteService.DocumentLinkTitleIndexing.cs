using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static DocumentLinkTitleIndex BuildDocumentLinkTitleIndex(
        IEnumerable<DocumentLinkNote> notes,
        ILogger<NoteService>? logger = null)
    {
        Dictionary<string, List<DocumentLinkNote>> notesByTitle = new(StringComparer.OrdinalIgnoreCase);
        int skippedShortTitleCount = 0;
        int noteCount = 0;
        foreach (DocumentLinkNote note in notes)
        {
            noteCount++;
            if (note.Title.Trim().Length < 3)
            {
                skippedShortTitleCount++;
                continue;
            }

            string titleKey = NormalizeDocumentTitleLookupKey(note.Title);
            if (!notesByTitle.TryGetValue(titleKey, out List<DocumentLinkNote>? candidates))
            {
                candidates = [];
                notesByTitle.Add(titleKey, candidates);
            }

            candidates.Add(note);
        }

        Dictionary<string, DocumentLinkNote> unambiguousTitles = [];
        Dictionary<string, IReadOnlyList<DocumentLinkNote>> ambiguousTitles = [];
        foreach (KeyValuePair<string, List<DocumentLinkNote>> pair in notesByTitle)
        {
            if (pair.Value.Count == 1)
            {
                unambiguousTitles[pair.Key] = pair.Value[0];
                continue;
            }

            ambiguousTitles[pair.Key] = pair.Value.ToArray();
        }

        return CreateDocumentLinkTitleIndexModel(unambiguousTitles, ambiguousTitles);
    }

    private static string NormalizeDocumentTitleLookupKey(string title)
    {
        return SearchableDocumentText.NormalizeWhitespace(title);
    }
}
