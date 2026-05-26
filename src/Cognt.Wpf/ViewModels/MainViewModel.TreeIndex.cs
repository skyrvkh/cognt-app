using System.IO;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private readonly Dictionary<long, NoteTreeNodeViewModel> loadedTreeNodesById = [];
    private readonly Dictionary<string, NoteTreeNodeViewModel> loadedTreeNodesByNotePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NoteTreeNodeViewModel> loadedTreeNodesByRelativePath = new(StringComparer.OrdinalIgnoreCase);

    internal bool TryGetLoadedTreePathById(long itemId, out IReadOnlyList<NoteTreeNodeViewModel>? path)
    {
        path = null;
        return loadedTreeNodesById.TryGetValue(itemId, out NoteTreeNodeViewModel? node)
            && TryBuildLoadedTreePath(node, out path);
    }

    internal bool TryGetLoadedTreePathByNotePath(string notePath, out IReadOnlyList<NoteTreeNodeViewModel>? path)
    {
        path = null;
        string? normalizedPath = NormalizeLoadedTreeNotePath(notePath);
        return normalizedPath is not null
            && loadedTreeNodesByNotePath.TryGetValue(normalizedPath, out NoteTreeNodeViewModel? node)
            && TryBuildLoadedTreePath(node, out path);
    }

    internal NoteTreeNodeViewModel? GetLoadedTreeNodeByRelativePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? null
            : loadedTreeNodesByRelativePath.GetValueOrDefault(NormalizeRelativePath(relativePath));
    }

    private void ResetLoadedTreeNodeIndex()
    {
        loadedTreeNodesById.Clear();
        loadedTreeNodesByNotePath.Clear();
        loadedTreeNodesByRelativePath.Clear();
    }

    private void IndexLoadedTreeNode(NoteTreeNodeViewModel node)
    {
        if (node.IsPlaceholder)
        {
            return;
        }

        loadedTreeNodesById[node.Id] = node;

        if (NormalizeLoadedTreeNotePath(node.NotePath) is { } normalizedPath)
        {
            loadedTreeNodesByNotePath[normalizedPath] = node;
        }

        if (!string.IsNullOrWhiteSpace(node.FileRelPath))
        {
            loadedTreeNodesByRelativePath[NormalizeRelativePath(node.FileRelPath)] = node;
        }
    }

    private void IndexLoadedTreeSubtree(NoteTreeNodeViewModel node)
    {
        IndexLoadedTreeNode(node);

        foreach (NoteTreeNodeViewModel child in node.Children)
        {
            IndexLoadedTreeSubtree(child);
        }
    }

    private void UnindexLoadedTreeSubtree(NoteTreeNodeViewModel node)
    {
        if (!node.IsPlaceholder)
        {
            loadedTreeNodesById.Remove(node.Id);

            if (NormalizeLoadedTreeNotePath(node.NotePath) is { } normalizedPath)
            {
                loadedTreeNodesByNotePath.Remove(normalizedPath);
            }

            if (!string.IsNullOrWhiteSpace(node.FileRelPath))
            {
                loadedTreeNodesByRelativePath.Remove(NormalizeRelativePath(node.FileRelPath));
            }
        }

        foreach (NoteTreeNodeViewModel child in node.Children)
        {
            UnindexLoadedTreeSubtree(child);
        }
    }

    private bool TryBuildLoadedTreePath(
        NoteTreeNodeViewModel node,
        out IReadOnlyList<NoteTreeNodeViewModel>? path)
    {
        List<NoteTreeNodeViewModel> reversePath = [];
        HashSet<long> visitedIds = [];
        NoteTreeNodeViewModel? current = node;

        while (current is not null)
        {
            if (!current.IsPlaceholder && !visitedIds.Add(current.Id))
            {
                path = null;
                return false;
            }

            reversePath.Add(current);

            if (current.ParentId is not long parentId)
            {
                reversePath.Reverse();
                path = reversePath;
                return true;
            }

            if (!loadedTreeNodesById.TryGetValue(parentId, out current))
            {
                path = null;
                return false;
            }
        }

        path = null;
        return false;
    }

    private static string? NormalizeLoadedTreeNotePath(string? notePath)
    {
        return string.IsNullOrWhiteSpace(notePath)
            ? null
            : Path.GetFullPath(notePath);
    }
}
