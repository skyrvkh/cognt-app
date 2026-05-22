using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<string> INoteService.LoadAvailableTags(string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        string[] assignedTags = repository.LoadTagNames().ToArray();
        string[] contentTags = GetOrBuildNoteCatalogEntries(root)
            .SelectMany(entry => entry.Item.ContentTags ?? Array.Empty<string>())
            .ToArray();

        return assignedTags
            .Concat(contentTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(NormalizeTagDisplayName)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    IReadOnlyList<string> INoteService.LoadAssignedTags(string collectionId, IReadOnlyList<long> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        return repository
            .LoadTagsByItemIds(itemIds)
            .Select(row => row.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        long[] normalizedRoots = rootItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        string[] normalizedTags = NormalizeTagInputs(tags);
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
        return new KnowledgeTagOperationResult(
            normalizedRoots.Length,
            affectedItemIds.Length,
            normalizedTags,
            includeDescendants,
            operation);
    }

    private static string[] NormalizeTagInputs(IReadOnlyList<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(NormalizeTagDisplayName)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }
}
