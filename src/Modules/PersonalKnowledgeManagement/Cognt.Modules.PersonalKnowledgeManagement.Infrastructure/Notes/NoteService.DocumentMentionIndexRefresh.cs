using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void RebuildDocumentMentionIndex(
        string root,
        ITreeRepository repository,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath,
        ILogger<NoteService>? logger = null)
    {
        foreach (DocumentLinkNote note in notesByPath.Values)
        {
            string notePath = Path.Combine(root, ToPlatformPath(note.RelativePath));
            if (!File.Exists(notePath))
            {
                continue;
            }

            ReplaceSourceDocumentMentions(
                root,
                repository,
                note,
                File.ReadAllText(notePath, Encoding.UTF8),
                notesByPath,
                logger);
        }
    }

    private static void ReplaceSourceDocumentMentions(
        string root,
        ITreeRepository repository,
        DocumentLinkNote sourceNote,
        string content,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath,
        ILogger<NoteService>? logger = null)
    {
        IReadOnlyList<DocumentMentionRow> mentions = ExtractDocumentMentions(
            root,
            sourceNote,
            content,
            notesByPath,
            logger);
        repository.ReplaceDocumentMentions(sourceNote.DocumentId, mentions);
    }
}
