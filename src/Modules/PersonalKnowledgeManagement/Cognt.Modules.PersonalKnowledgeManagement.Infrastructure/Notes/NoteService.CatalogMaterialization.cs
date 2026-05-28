using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private IReadOnlyList<NoteCatalogEntry> BuildNoteCatalogEntries(string root)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        IReadOnlyList<KnowledgeTreeItemRow> noteItems = repository.LoadNoteItems();

        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId =
            NoteCatalogAttachmentIndex.LoadByNoteId(repository);
        IReadOnlyDictionary<long, IReadOnlyList<string>> assignedTagsByItemId =
            NoteCatalogAssignedTagIndex.LoadByItemId(repository, noteItems);

        List<NoteCatalogEntry> items = new(noteItems.Count);

        foreach (KnowledgeTreeItemRow row in noteItems)
        {
            NoteCatalogEntry? entry = TryCreateNoteCatalogEntry(
                root,
                row,
                attachmentsByNoteId,
                assignedTagsByItemId,
                logger);
            if (entry is not null)
            {
                items.Add(entry);
            }
        }

        return items.ToArray();
    }
}
