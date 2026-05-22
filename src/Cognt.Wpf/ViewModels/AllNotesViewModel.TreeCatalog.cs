using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private AllNotesTreeItemViewModel[] BuildTreeItems(long? parentId, int depth)
    {
        IReadOnlyList<KnowledgeTreeItemReadModel> items = parentId.HasValue
            ? noteService.LoadChildren(currentCollectionId, parentId.Value)
            : noteService.LoadRootItems(currentCollectionId);

        return items
            .Select(item =>
            {
                AllNotesTreeItemViewModel[] children = CanTreeItemContainChildren(item.Kind) && item.ChildCount > 0
                    ? BuildTreeItems(item.Id, depth + 1)
                    : [];

                return new AllNotesTreeItemViewModel(
                    item.Id,
                    item.Kind,
                    item.Title,
                    NormalizeTreeItemRelativePath(item.FileRelPath),
                    GetTreeTypeLabel(item),
                    GetTreeTagsLabel(item),
                    GetTreeCardCount(item),
                    GetTreeCardCountLabel(item),
                    GetTreeCreatedAtLabel(item),
                    GetTreeModifiedAtLabel(item),
                    children)
                {
                    IsExpanded = depth == 0
                };
            })
            .ToArray();
    }

    private void ReloadVisibleTreeNodes(IReadOnlySet<string> visibleRelativePaths, bool hasActiveFilters)
    {
        VisibleTreeNodes.Clear();

        foreach (AllNotesTreeItemViewModel root in allTreeRoots)
        {
            AllNotesTreeItemViewModel? visibleRoot = CreateVisibleTreeNode(
                root,
                visibleRelativePaths,
                hasActiveFilters);

            if (visibleRoot is not null)
            {
                VisibleTreeNodes.Add(visibleRoot);
            }
        }
    }

    private static AllNotesTreeItemViewModel? CreateVisibleTreeNode(
        AllNotesTreeItemViewModel node,
        IReadOnlySet<string> visibleRelativePaths,
        bool hasActiveFilters)
    {
        AllNotesTreeItemViewModel[] children = node.Children
            .Select(child => CreateVisibleTreeNode(child, visibleRelativePaths, hasActiveFilters))
            .OfType<AllNotesTreeItemViewModel>()
            .ToArray();

        bool isMatchingNote = node.CanOpen
            && node.RelativePath is not null
            && visibleRelativePaths.Contains(node.RelativePath);
        bool includeNode = hasActiveFilters
            ? isMatchingNote || children.Length > 0
            : true;

        if (!includeNode)
        {
            return null;
        }

        return new AllNotesTreeItemViewModel(
            node.Id,
            node.Kind,
            node.Title,
            node.RelativePath,
            node.TypeLabel,
            node.TagsLabel,
            node.CardCount,
            node.CardCountLabel,
            node.CreatedAtLabel,
            node.ModifiedAtLabel,
            children)
        {
            IsExpanded = hasActiveFilters || node.IsExpanded
        };
    }

    private string GetTreeCreatedAtLabel(KnowledgeTreeItemReadModel item)
    {
        return TryFormatTreeTimestamp(item, static note => note.CreatedAt);
    }

    private string GetTreeModifiedAtLabel(KnowledgeTreeItemReadModel item)
    {
        return TryFormatTreeTimestamp(item, static note => note.UpdatedAt);
    }

    private string GetTreeTypeLabel(KnowledgeTreeItemReadModel item)
    {
        string? relativePath = NormalizeTreeItemRelativePath(item.FileRelPath);
        if (relativePath is not null
            && noteLookupByRelativePath.TryGetValue(relativePath, out KnowledgeNoteListItemReadModel? note))
        {
            return GetTypeLabel(note.Type);
        }

        return item.Kind switch
        {
            KnowledgeItemKind.Folder => "Folder",
            KnowledgeItemKind.PdfDocument => "PDF",
            KnowledgeItemKind.Trash => "Trash",
            KnowledgeItemKind.Root => "Workspace",
            _ => "Note"
        };
    }

    private string GetTreeTagsLabel(KnowledgeTreeItemReadModel item)
    {
        string? relativePath = NormalizeTreeItemRelativePath(item.FileRelPath);
        if (relativePath is null || !noteLookupByRelativePath.TryGetValue(relativePath, out KnowledgeNoteListItemReadModel? note))
        {
            return string.Empty;
        }

        return note.Tags.Count == 0
            ? string.Empty
            : string.Join(", ", note.Tags);
    }

    private string GetTreeCardCountLabel(KnowledgeTreeItemReadModel item)
    {
        return FormatCardCount(GetTreeCardCount(item));
    }

    private int GetTreeCardCount(KnowledgeTreeItemReadModel item)
    {
        string? relativePath = NormalizeTreeItemRelativePath(item.FileRelPath);
        return GetCardCount(relativePath);
    }

    private string TryFormatTreeTimestamp(
        KnowledgeTreeItemReadModel item,
        Func<KnowledgeNoteListItemReadModel, DateTimeOffset> selector)
    {
        string? relativePath = NormalizeTreeItemRelativePath(item.FileRelPath);
        if (relativePath is null || !noteLookupByRelativePath.TryGetValue(relativePath, out KnowledgeNoteListItemReadModel? note))
        {
            return string.Empty;
        }

        return FormatTimestamp(selector(note));
    }

    private static bool CanTreeItemContainChildren(KnowledgeItemKind kind)
    {
        return kind is KnowledgeItemKind.Folder
            or KnowledgeItemKind.Note
            or KnowledgeItemKind.Root
            or KnowledgeItemKind.Trash;
    }

    private static string? NormalizeTreeItemRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? null
            : relativePath.Replace('\\', '/').Trim('/');
    }
}
