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
        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId = LoadCatalogAttachmentsByNoteId(repository);
        IReadOnlyDictionary<long, IReadOnlyList<string>> assignedTagsByItemId = repository
            .LoadTagsByItemIds(noteItems.Select(item => item.Id).ToArray())
            .GroupBy(row => row.ItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        List<NoteCatalogEntry> items = [];

        foreach (KnowledgeTreeItemRow row in noteItems)
        {
            NoteCatalogEntry? entry = TryCreateNoteCatalogEntry(root, row, attachmentsByNoteId, assignedTagsByItemId);
            if (entry is not null)
            {
                items.Add(entry);
            }
        }

        return items.ToArray();
    }
}
