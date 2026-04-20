using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<string> CollectDeletedNotePaths(
        string root,
        ITreeRepository repository,
        long itemId)
    {
        KnowledgeTreeItemRow item = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
        List<string> notePaths = [];
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

        CollectDeletedNotePaths(root, repository, item, notePaths, seenPaths);
        return notePaths;
    }

    private static void CollectDeletedNotePaths(
        string root,
        ITreeRepository repository,
        KnowledgeTreeItemRow item,
        ICollection<string> notePaths,
        ISet<string> seenPaths)
    {
        if (item.Kind == KnowledgeTreeItemKind.Note && !string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            string notePath = ResolveNotePathForDeletion(root, item.FileRelPath);
            if (seenPaths.Add(notePath))
            {
                notePaths.Add(notePath);
            }
        }

        foreach (KnowledgeTreeItemRow child in repository.LoadChildren(item.Id))
        {
            CollectDeletedNotePaths(root, repository, child, notePaths, seenPaths);
        }
    }

    private static string ResolveNotePathForDeletion(string root, string fileRelPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(fileRelPath)));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new InvalidOperationException($"The note file path is outside the collection: {fileRelPath}");
        }

        return fullPath;
    }
}
