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
        return LoadMentionTargets(
            collectionId,
            static repository => repository.LoadNoteItems());
    }

    IReadOnlyList<KnowledgeMentionTargetReadModel> INoteService.SearchMentionTargets(
        string collectionId,
        string query,
        int limit)
    {
        return LoadMentionTargets(
            collectionId,
            repository => repository.SearchNoteItems(query, limit));
    }

    private IReadOnlyList<KnowledgeMentionTargetReadModel> LoadMentionTargets(
        string collectionId,
        Func<ITreeRepository, IEnumerable<KnowledgeTreeItemRow>> loadItems)
    {
        ArgumentNullException.ThrowIfNull(loadItems);

        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            return [];
        }

        string root = NormalizeRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);

        return loadItems(repository)
            .Where(item => !string.IsNullOrWhiteSpace(item.FileRelPath))
            .Select(item => CreateMentionTarget(root, item))
            .OfType<KnowledgeMentionTargetReadModel>()
            .ToArray();
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
