using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private string? cachedNoteCatalogRoot;
    private IReadOnlyList<NoteCatalogEntry>? cachedNoteCatalogEntries;

    public IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(
        string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        return GetOrBuildNoteCatalogEntries(root)
            .Select(entry => entry.Item)
            .ToArray();
    }

    public IReadOnlySet<string> SearchNoteCatalog(
        string collectionId,
        string query)
    {
        string normalizedQuery = SearchableDocumentText.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        string root = GetExistingCollectionRoot(collectionId);
        return GetOrBuildNoteCatalogEntries(root)
            .Where(entry => entry.SearchText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Item.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<NoteCatalogEntry> GetOrBuildNoteCatalogEntries(string root)
    {
        if (cachedNoteCatalogEntries is not null
            && string.Equals(cachedNoteCatalogRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            return cachedNoteCatalogEntries;
        }

        IReadOnlyList<NoteCatalogEntry> entries = BuildNoteCatalogEntries(root);
        cachedNoteCatalogRoot = root;
        cachedNoteCatalogEntries = entries;
        return entries;
    }

    private void InvalidateNoteCatalogCache(string? root = null)
    {
        if (cachedNoteCatalogEntries is null)
        {
            return;
        }

        if (root is null || string.Equals(cachedNoteCatalogRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            cachedNoteCatalogRoot = null;
            cachedNoteCatalogEntries = null;
        }
    }
}
