using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static Dictionary<string, DocumentLinkNote> LoadDocumentLinkNotesByRelativePath(
        string root,
        ITreeRepository repository)
    {
        IReadOnlyList<KnowledgeTreeItemRow> items = repository.LoadNoteItems();
        Dictionary<string, DocumentLinkNote> notesByPath = new(StringComparer.OrdinalIgnoreCase);
        foreach (KnowledgeTreeItemRow item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FileRelPath))
            {
                continue;
            }

            DocumentLinkNote? note = TryCreateDocumentLinkNote(root, item);
            if (note is not null)
            {
                notesByPath.Add(note.RelativePath, note);
            }
        }

        return notesByPath;
    }

    private static DocumentLinkNote? TryCreateDocumentLinkNote(
        string root,
        KnowledgeTreeItemRow item)
    {
        if (string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            return null;
        }

        string relativePath = NormalizeRelativePath(item.FileRelPath);
        string fullPath = Path.Combine(root, ToPlatformPath(relativePath));
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return new DocumentLinkNote(
            item.Uid,
            relativePath,
            string.IsNullOrWhiteSpace(item.Title) ? Path.GetFileNameWithoutExtension(fullPath) : item.Title,
            item.UpdatedAt);
    }
}
