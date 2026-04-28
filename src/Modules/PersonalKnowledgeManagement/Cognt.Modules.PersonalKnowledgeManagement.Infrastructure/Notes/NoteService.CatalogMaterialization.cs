using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private IReadOnlyList<NoteCatalogEntry> BuildNoteCatalogEntries(string root)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId = LoadCatalogAttachmentsByNoteId(repository);
        List<NoteCatalogEntry> items = [];

        foreach (KnowledgeTreeItemRow row in repository.LoadNoteItems())
        {
            NoteCatalogEntry? entry = TryCreateNoteCatalogEntry(root, row, attachmentsByNoteId);
            if (entry is not null)
            {
                items.Add(entry);
            }
        }

        return items.ToArray();
    }
}
