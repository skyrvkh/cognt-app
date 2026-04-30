using System.Collections.ObjectModel;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private ObservableCollection<NoteTreeNodeViewModel>? FindLoadedSiblingCollection(long? parentId)
    {
        if (parentId is null)
        {
            return NoteRoots;
        }

        NoteTreeNodeViewModel? parent = FindTreeNode(parentId.Value);
        return parent is { AreChildrenLoaded: true } ? parent.Children : null;
    }

    private ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(NoteTreeNodeViewModel target)
    {
        return FindOwningCollection(NoteRoots, target);
    }

    private static ObservableCollection<NoteTreeNodeViewModel>? FindOwningCollection(
        ObservableCollection<NoteTreeNodeViewModel> nodes,
        NoteTreeNodeViewModel target)
    {
        if (nodes.Contains(target))
        {
            return nodes;
        }

        foreach (NoteTreeNodeViewModel node in nodes)
        {
            ObservableCollection<NoteTreeNodeViewModel>? result = FindOwningCollection(node.Children, target);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
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
        return FindTreeNode(NoteRoots, id);
    }

    private static NoteTreeNodeViewModel? FindTreeNode(IEnumerable<NoteTreeNodeViewModel> nodes, long id)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (!node.IsPlaceholder && node.Id == id)
            {
                return node;
            }

            NoteTreeNodeViewModel? result = FindTreeNode(node.Children, id);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
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
