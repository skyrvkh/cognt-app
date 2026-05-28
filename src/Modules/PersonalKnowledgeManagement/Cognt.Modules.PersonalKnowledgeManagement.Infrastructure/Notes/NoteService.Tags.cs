using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<string> INoteService.LoadAvailableTags(string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> availableTags = [];

        KnowledgeTagNormalizer.AddDisplayNames(repository.LoadTagNames(), seenTags, availableTags);
        foreach (NoteCatalogEntry entry in GetOrBuildNoteCatalogEntries(root))
        {
            KnowledgeTagNormalizer.AddDisplayNames(entry.Item.ContentTags, seenTags, availableTags);
        }

        availableTags.Sort(StringComparer.OrdinalIgnoreCase);
        return availableTags.ToArray();
    }

    IReadOnlyList<string> INoteService.LoadAssignedTags(string collectionId, IReadOnlyList<long> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> assignedTags = [];
        foreach (var row in repository.LoadTagsByItemIds(itemIds))
        {
            if (seenTags.Add(row.DisplayName))
            {
                assignedTags.Add(row.DisplayName);
            }
        }

        assignedTags.Sort(StringComparer.OrdinalIgnoreCase);
        return assignedTags.ToArray();
    }

    KnowledgeTagOperationResult INoteService.AssignTags(
        string collectionId,
        IReadOnlyList<long> rootItemIds,
        IReadOnlyList<string> tags,
        bool includeDescendants)
    {
        return ApplyTags(collectionId, rootItemIds, tags, includeDescendants, KnowledgeTagOperationKind.Add);
    }

    KnowledgeTagOperationResult INoteService.RemoveTags(
        string collectionId,
        IReadOnlyList<long> rootItemIds,
        IReadOnlyList<string> tags,
        bool includeDescendants)
    {
        return ApplyTags(collectionId, rootItemIds, tags, includeDescendants, KnowledgeTagOperationKind.Remove);
    }

    private KnowledgeTagOperationResult ApplyTags(
        string collectionId,
        IReadOnlyList<long> rootItemIds,
        IReadOnlyList<string> tags,
        bool includeDescendants,
        KnowledgeTagOperationKind operation)
    {
        ArgumentNullException.ThrowIfNull(rootItemIds);
        ArgumentNullException.ThrowIfNull(tags);

        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        long[] normalizedRoots = NormalizeRootItemIds(rootItemIds);
        string[] normalizedTags = KnowledgeTagNormalizer.NormalizeDisplayNames(tags);
        long[] affectedItemIds = repository.ExpandItemIds(normalizedRoots, includeDescendants).ToArray();

        if (operation == KnowledgeTagOperationKind.Add)
        {
            repository.AssignTagsToItems(affectedItemIds, normalizedTags);
        }
        else
        {
            repository.RemoveTagsFromItems(affectedItemIds, normalizedTags);
        }

        InvalidateNoteCatalogCache(root);
        KnowledgeTagOperationResult result = new(
            normalizedRoots.Length,
            affectedItemIds.Length,
            normalizedTags,
            includeDescendants,
            operation);
        return result;
    }

    private static long[] NormalizeRootItemIds(IReadOnlyList<long> rootItemIds)
    {
        HashSet<long> seenItemIds = [];
        List<long> normalizedItemIds = new(rootItemIds.Count);
        foreach (long itemId in rootItemIds)
        {
            if (itemId > 0 && seenItemIds.Add(itemId))
            {
                normalizedItemIds.Add(itemId);
            }
        }

        normalizedItemIds.Sort();
        return normalizedItemIds.ToArray();
    }
}
