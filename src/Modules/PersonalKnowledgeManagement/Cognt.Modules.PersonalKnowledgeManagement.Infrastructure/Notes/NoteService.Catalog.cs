using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<KnowledgeNoteListItemReadModel> INoteService.LoadNoteCatalog(
        string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        IReadOnlyList<NoteCatalogEntry> entries = GetOrBuildNoteCatalogEntries(root);
        KnowledgeNoteListItemReadModel[] items = new KnowledgeNoteListItemReadModel[entries.Count];
        for (int index = 0; index < items.Length; index++)
        {
            items[index] = entries[index].Item;
        }

        return items;
    }

    IReadOnlySet<string> INoteService.SearchNoteCatalog(
        string collectionId,
        string query)
    {
        string normalizedQuery = SearchableDocumentText.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        string root = GetExistingCollectionRoot(collectionId);
        IReadOnlyList<NoteCatalogEntry> entries = GetOrBuildNoteCatalogEntries(root);
        HashSet<string> matches = new(StringComparer.OrdinalIgnoreCase);
        foreach (NoteCatalogEntry entry in entries)
        {
            if (entry.SearchText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(entry.Item.RelativePath);
            }
        }

        return matches;
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
