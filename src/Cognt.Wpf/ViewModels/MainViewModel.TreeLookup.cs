using System.Collections.ObjectModel;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal IReadOnlyList<NoteTreeNodeViewModel>? GetLoadedSiblingList(NoteTreeNodeViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        IReadOnlyList<NoteTreeNodeViewModel>? siblings = FindOwningCollection(target);
        return siblings;
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindLoadedSiblingCollection(long? parentId)
    {
        if (parentId is null)
        {
            return NoteRoots;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        ObservableCollection<NoteTreeNodeViewModel>? siblings = parent is { AreChildrenLoaded: true } ? parent.Children : null;
        return siblings;
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(NoteTreeNodeViewModel target)
    {
        if (target.ParentId is null)
        {
            ObservableCollection<NoteTreeNodeViewModel>? roots = NoteRoots.Contains(target)
                ? NoteRoots
                : null;
            return roots;
        }

        ObservableCollection<NoteTreeNodeViewModel>? siblings = FindTreeNode(target.ParentId.Value) is { AreChildrenLoaded: true } parent
            && parent.Children.Contains(target)
                ? parent.Children
                : null;
        return siblings;
    }

    private static void RemoveNodeById(ObservableCollection<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            NoteTreeNodeViewModel node = nodes[index];
            if (!node.IsPlaceholder && node.Id == id)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    private NoteTreeNodeViewModel? FindTreeNode(long id)
    {
        NoteTreeNodeViewModel? node = loadedTreeNodesById.GetValueOrDefault(id);
        return node;
    }

    private static int IndexOfNode(IReadOnlyList<NoteTreeNodeViewModel> nodes, long id)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (!nodes[index].IsPlaceholder && nodes[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void RemovePlaceholders(ObservableCollection<NoteTreeNodeViewModel> nodes)
    {
        for (int index = nodes.Count - 1; index >= 0; index--)
        {
            if (nodes[index].IsPlaceholder)
            {
                nodes.RemoveAt(index);
            }
        }
    }
}
