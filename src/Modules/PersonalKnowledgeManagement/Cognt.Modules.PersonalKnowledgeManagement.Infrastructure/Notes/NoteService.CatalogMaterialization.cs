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
        IReadOnlyDictionary<long, IReadOnlyList<string>> assignedTagsByItemId = LoadCatalogAssignedTagsByItemId(repository, noteItems);
        List<NoteCatalogEntry> items = new(noteItems.Count);

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

    private static IReadOnlyDictionary<long, IReadOnlyList<string>> LoadCatalogAssignedTagsByItemId(
        ITreeRepository repository,
        IReadOnlyList<KnowledgeTreeItemRow> noteItems)
    {
        if (noteItems.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<string>>();
        }

        long[] itemIds = new long[noteItems.Count];
        for (int index = 0; index < itemIds.Length; index++)
        {
            itemIds[index] = noteItems[index].Id;
        }

        Dictionary<long, TagNameAccumulator> tagsByItemId = [];
        foreach (ItemTagRow row in repository.LoadTagsByItemIds(itemIds))
        {
            if (!tagsByItemId.TryGetValue(row.ItemId, out TagNameAccumulator? accumulator))
            {
                accumulator = new TagNameAccumulator();
                tagsByItemId.Add(row.ItemId, accumulator);
            }

            accumulator.Add(row.DisplayName);
        }

        Dictionary<long, IReadOnlyList<string>> assignedTagsByItemId = new(tagsByItemId.Count);
        foreach (KeyValuePair<long, TagNameAccumulator> pair in tagsByItemId)
        {
            assignedTagsByItemId.Add(pair.Key, pair.Value.ToSortedArray());
        }

        return assignedTagsByItemId;
    }

    private sealed class TagNameAccumulator
    {
        private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> values = [];

        internal void Add(string value)
        {
            if (seen.Add(value))
            {
                values.Add(value);
            }
        }

        internal string[] ToSortedArray()
        {
            values.Sort(StringComparer.OrdinalIgnoreCase);
            return values.ToArray();
        }
    }
}
