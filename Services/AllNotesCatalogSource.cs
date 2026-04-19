using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Models.Tree;
using CogntApp.ViewModels;

namespace CogntApp.Services;

public sealed class AllNotesCatalogSource : IAllNotesCatalogSource
{
    private readonly INoteService noteService;

    public AllNotesCatalogSource(INoteService noteService)
    {
        ArgumentNullException.ThrowIfNull(noteService);

        this.noteService = noteService;
    }

    public IReadOnlyList<KnowledgeTreeItemReadModel> LoadRootItems(string collectionId)
    {
        return noteService.LoadRootItems(collectionId)
            .Select(ToReadModel)
            .ToArray();
    }

    public IReadOnlyList<KnowledgeTreeItemReadModel> LoadChildren(string collectionId, long parentId)
    {
        return noteService.LoadChildren(collectionId, parentId)
            .Select(ToReadModel)
            .ToArray();
    }

    public IReadOnlySet<string> SearchNoteCatalog(string collectionId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LoadNoteCatalog(collectionId)
                .Select(note => note.RelativePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        string normalizedQuery = query.Trim();
        HashSet<string> matches = new(StringComparer.OrdinalIgnoreCase);

        foreach (TreeItemRow item in EnumerateItems(collectionId, parentId: null))
        {
            if (!IsFileBackedItem(item) || string.IsNullOrWhiteSpace(item.FileRelPath))
            {
                continue;
            }

            string relativePath = NormalizeRelativePath(item.FileRelPath);
            if (item.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(relativePath);
                continue;
            }

            string fullPath = Path.Combine(collectionId, ToPlatformPath(relativePath));
            if (File.Exists(fullPath)
                && File.ReadAllText(fullPath).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(relativePath);
            }
        }

        return matches;
    }

    public IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(string collectionId)
    {
        return EnumerateItems(collectionId, parentId: null)
            .Where(item => IsFileBackedItem(item) && !string.IsNullOrWhiteSpace(item.FileRelPath))
            .Select(ToCatalogItem)
            .ToArray();
    }

    private IEnumerable<TreeItemRow> EnumerateItems(string collectionId, long? parentId)
    {
        IReadOnlyList<TreeItemRow> items = parentId.HasValue
            ? noteService.LoadChildren(collectionId, parentId.Value)
            : noteService.LoadRootItems(collectionId);

        foreach (TreeItemRow item in items)
        {
            yield return item;

            if (item.ChildCount <= 0 || item.Kind is not (TreeItemKind.Folder or TreeItemKind.Note or TreeItemKind.PdfDocument or TreeItemKind.Root or TreeItemKind.Trash))
            {
                continue;
            }

            foreach (TreeItemRow child in EnumerateItems(collectionId, item.Id))
            {
                yield return child;
            }
        }
    }

    private static KnowledgeTreeItemReadModel ToReadModel(TreeItemRow item)
    {
        return new KnowledgeTreeItemReadModel(
            item.Id,
            item.ParentId,
            ToKnowledgeKind(item.Kind),
            item.Title,
            item.FileRelPath,
            item.ChildCount);
    }

    private static KnowledgeNoteListItemReadModel ToCatalogItem(TreeItemRow item)
    {
        string relativePath = NormalizeRelativePath(item.FileRelPath ?? string.Empty);
        return new KnowledgeNoteListItemReadModel(
            item.Title,
            relativePath,
            GetNoteType(relativePath),
            [],
            ToTimestamp(item.CreatedAt),
            ToTimestamp(item.UpdatedAt));
    }

    private static KnowledgeItemKind ToKnowledgeKind(TreeItemKind kind)
    {
        return kind switch
        {
            TreeItemKind.Folder => KnowledgeItemKind.Folder,
            TreeItemKind.Root => KnowledgeItemKind.Root,
            TreeItemKind.Trash => KnowledgeItemKind.Trash,
            TreeItemKind.PdfDocument => KnowledgeItemKind.PdfDocument,
            _ => KnowledgeItemKind.Note
        };
    }

    private static bool IsFileBackedItem(TreeItemRow item)
    {
        return item.Kind is TreeItemKind.Note or TreeItemKind.PdfDocument;
    }

    private static KnowledgeNoteListItemType GetNoteType(string relativePath)
    {
        return string.Equals(Path.GetExtension(relativePath), ".md", StringComparison.OrdinalIgnoreCase)
            ? KnowledgeNoteListItemType.Markdown
            : KnowledgeNoteListItemType.Note;
    }

    private static DateTimeOffset ToTimestamp(long value)
    {
        return value > 100_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }

    private static string ToPlatformPath(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
