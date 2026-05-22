using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<KnowledgeMentionTargetReadModel> INoteService.ListMentionTargets(
        string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            return [];
        }

        string root = NormalizeRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        EnsureAllPdfAttachmentTreeItems(repository, preserveDeletedTreeItems: true);

        return LoadMentionableTreeItems(repository)
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FileRelPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => CreateMentionTarget(root, item))
            .OfType<KnowledgeMentionTargetReadModel>()
            .DistinctBy(target => NormalizeRelativePath(target.RelativePath), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    IReadOnlyList<KnowledgeMentionTargetReadModel> INoteService.SearchMentionTargets(
        string collectionId,
        string query,
        int limit)
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            return [];
        }

        string root = NormalizeRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        EnsureAllPdfAttachmentTreeItems(repository, preserveDeletedTreeItems: true);
        string normalizedQuery = query.Trim();
        int boundedLimit = Math.Clamp(limit, 1, 64);

        return LoadMentionableTreeItems(repository)
            .Where(item => MatchesMentionSearch(item, normalizedQuery))
            .OrderBy(item => GetMentionSearchRank(item, normalizedQuery))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FileRelPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => CreateMentionTarget(root, item))
            .OfType<KnowledgeMentionTargetReadModel>()
            .DistinctBy(target => NormalizeRelativePath(target.RelativePath), StringComparer.OrdinalIgnoreCase)
            .Take(boundedLimit)
            .ToArray();
    }

    private static IEnumerable<KnowledgeTreeItemRow> LoadMentionableTreeItems(ITreeRepository repository)
    {
        Stack<KnowledgeTreeItemRow> pending = new(repository.LoadRootItems().Reverse());
        HashSet<long> visited = [];

        while (pending.Count > 0)
        {
            KnowledgeTreeItemRow item = pending.Pop();
            if (!visited.Add(item.Id) || item.IsDeleted)
            {
                continue;
            }

            if (IsMentionableTreeItem(item))
            {
                yield return item;
            }

            foreach (KnowledgeTreeItemRow child in repository.LoadChildren(item.Id).Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsMentionableTreeItem(KnowledgeTreeItemRow item)
    {
        return item.Kind is KnowledgeTreeItemKind.Note or KnowledgeTreeItemKind.PdfDocument
            && !string.IsNullOrWhiteSpace(item.FileRelPath);
    }

    private static bool MatchesMentionSearch(KnowledgeTreeItemRow item, string query)
    {
        return query.Length == 0
            || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (item.FileRelPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static int GetMentionSearchRank(KnowledgeTreeItemRow item, string query)
    {
        if (query.Length == 0)
        {
            return 4;
        }

        if (string.Equals(item.Title, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return item.FileRelPath?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true
            ? 2
            : 3;
    }

    private static KnowledgeMentionTargetReadModel? CreateMentionTarget(
        string root,
        KnowledgeTreeItemRow item)
    {
        if (string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            return null;
        }

        string relativePath = NormalizeRelativePath(item.FileRelPath);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathInsideRoot(fullPath, root) || !File.Exists(fullPath))
        {
            return null;
        }

        string title = string.IsNullOrWhiteSpace(item.Title)
            ? Path.GetFileNameWithoutExtension(fullPath)
            : item.Title;

        return new KnowledgeMentionTargetReadModel(
            relativePath,
            title);
    }
}
