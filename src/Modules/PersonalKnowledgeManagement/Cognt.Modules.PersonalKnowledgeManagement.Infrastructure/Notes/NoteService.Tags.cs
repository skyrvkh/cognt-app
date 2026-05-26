using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<string> INoteService.LoadAvailableTags(string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> availableTags = [];

        AddNormalizedTags(repository.LoadTagNames(), seenTags, availableTags);
        foreach (NoteCatalogEntry entry in GetOrBuildNoteCatalogEntries(root))
        {
            AddNormalizedTags(entry.Item.ContentTags, seenTags, availableTags);
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
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedTags = [];
        AddNormalizedTags(tags, seenTags, normalizedTags);
        normalizedTags.Sort(StringComparer.OrdinalIgnoreCase);
        return normalizedTags.ToArray();
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

    private static void AddNormalizedTags(
        IEnumerable<string>? source,
        HashSet<string> seenTags,
        List<string> normalizedTags)
    {
        if (source is null)
        {
            return;
        }

        foreach (string tag in source)
        {
            string normalizedTag = NormalizeTagDisplayName(tag);
            if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
            {
                normalizedTags.Add(normalizedTag);
            }
        }
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedTag = tag.AsSpan().Trim();
        int firstWhitespaceIndex = IndexOfWhitespace(trimmedTag);
        if (firstWhitespaceIndex < 0)
        {
            return trimmedTag.ToString();
        }

        StringBuilder builder = new(trimmedTag.Length);
        builder.Append(trimmedTag[..firstWhitespaceIndex]);
        for (int index = firstWhitespaceIndex + 1; index < trimmedTag.Length; index++)
        {
            char character = trimmedTag[index];
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static int IndexOfWhitespace(ReadOnlySpan<char> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
