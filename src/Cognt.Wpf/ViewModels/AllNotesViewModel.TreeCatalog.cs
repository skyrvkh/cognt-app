using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private AllNotesTreeItemViewModel[] BuildTreeItems(long? parentId, int depth)
    {
        IReadOnlyList<KnowledgeTreeItemReadModel> items = parentId.HasValue
            ? noteService.LoadChildren(currentCollectionId, parentId.Value)
            : noteService.LoadRootItems(currentCollectionId);

        AllNotesTreeItemViewModel[] treeItems = new AllNotesTreeItemViewModel[items.Count];
        for (int index = 0; index < items.Count; index++)
        {
            KnowledgeTreeItemReadModel item = items[index];
            TreeItemPresentation presentation = CreateTreeItemPresentation(item);
            AllNotesTreeItemViewModel[] children = CanTreeItemContainChildren(item.Kind) && item.ChildCount > 0
                ? BuildTreeItems(item.Id, depth + 1)
                : [];

            treeItems[index] = new AllNotesTreeItemViewModel(
                item.Id,
                item.Kind,
                item.Title,
                presentation.RelativePath,
                presentation.TypeLabel,
                presentation.TagsLabel,
                presentation.CardCount,
                presentation.CardCountLabel,
                presentation.CreatedAtLabel,
                presentation.ModifiedAtLabel,
                children)
            {
                IsExpanded = depth == 0
            };
        }

        return treeItems;
    }

    private void ReloadVisibleTreeNodes(IReadOnlySet<string>? visibleRelativePaths, bool hasActiveFilters)
    {
        if (!hasActiveFilters)
        {
            SynchronizeObservableCollection(VisibleTreeNodes, allTreeRoots);
            return;
        }

        if (visibleRelativePaths is null)
        {
            SynchronizeObservableCollection(VisibleTreeNodes, []);
            return;
        }

        List<AllNotesTreeItemViewModel> visibleRoots = [];
        foreach (AllNotesTreeItemViewModel root in allTreeRoots)
        {
            AllNotesTreeItemViewModel? visibleRoot = CreateVisibleTreeNode(
                root,
                visibleRelativePaths,
                hasActiveFilters);

            if (visibleRoot is not null)
            {
                visibleRoots.Add(visibleRoot);
            }
        }

        SynchronizeObservableCollection(VisibleTreeNodes, visibleRoots);
    }
    private static AllNotesTreeItemViewModel? CreateVisibleTreeNode(
        AllNotesTreeItemViewModel node,
        IReadOnlySet<string> visibleRelativePaths,
        bool hasActiveFilters)
    {
        List<AllNotesTreeItemViewModel> children = [];
        foreach (AllNotesTreeItemViewModel child in node.Children)
        {
            AllNotesTreeItemViewModel? visibleChild = CreateVisibleTreeNode(
                child,
                visibleRelativePaths,
                hasActiveFilters);
            if (visibleChild is not null)
            {
                children.Add(visibleChild);
            }
        }

        bool isMatchingNote = node.CanOpen
            && node.RelativePath is not null
            && visibleRelativePaths.Contains(node.RelativePath);
        bool includeNode = hasActiveFilters
            ? isMatchingNote || children.Count > 0
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

    private TreeItemPresentation CreateTreeItemPresentation(KnowledgeTreeItemReadModel item)
    {
        string? relativePath = NormalizeTreeItemRelativePath(item.FileRelPath);
        KnowledgeNoteListItemReadModel? note = TryGetTreeNote(relativePath);
        int cardCount = GetCardCount(relativePath);

        return new TreeItemPresentation(
            relativePath,
            note is null ? GetTreeFallbackTypeLabel(item.Kind) : GetTypeLabel(note.Type),
            CreateTreeTagsLabel(note),
            cardCount,
            FormatCardCount(cardCount),
            note is null ? string.Empty : FormatTimestamp(note.CreatedAt),
            note is null ? string.Empty : FormatTimestamp(note.UpdatedAt));
    }

    private KnowledgeNoteListItemReadModel? TryGetTreeNote(string? relativePath)
    {
        if (relativePath is null)
        {
            return null;
        }

        return noteLookupByRelativePath.TryGetValue(relativePath, out KnowledgeNoteListItemReadModel? note)
            ? note
            : null;
    }

    private static string CreateTreeTagsLabel(KnowledgeNoteListItemReadModel? note)
    {
        return note is null || note.Tags.Count == 0
            ? string.Empty
            : string.Join(", ", note.Tags);
    }

    private static string GetTreeFallbackTypeLabel(KnowledgeItemKind kind)
    {
        return kind switch
        {
            KnowledgeItemKind.Folder => "Folder",
            KnowledgeItemKind.PdfDocument => "PDF",
            KnowledgeItemKind.Trash => "Trash",
            KnowledgeItemKind.Root => "Workspace",
            _ => "Note"
        };
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

    private readonly record struct TreeItemPresentation(
        string? RelativePath,
        string TypeLabel,
        string TagsLabel,
        int CardCount,
        string CardCountLabel,
        string CreatedAtLabel,
        string ModifiedAtLabel);
}
