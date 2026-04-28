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
        return repository.LoadNoteItems()
            .Where(item => !string.IsNullOrWhiteSpace(item.FileRelPath))
            .Select(item => TryCreateDocumentLinkNote(root, item))
            .Where(note => note is not null)
            .ToDictionary(note => note!.RelativePath, note => note!, StringComparer.OrdinalIgnoreCase);
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
