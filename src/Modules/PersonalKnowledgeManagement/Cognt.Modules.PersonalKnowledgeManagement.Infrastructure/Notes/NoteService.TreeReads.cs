using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    public IReadOnlyList<KnowledgeTreeItemReadModel> LoadRootItems(string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);

        return repository.LoadRootItems()
            .Select(ToTreeItemReadModel)
            .ToArray();
    }

    public IReadOnlyList<KnowledgeTreeItemReadModel> LoadChildren(string collectionId, long parentId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);

        return repository.LoadChildren(parentId)
            .Select(ToTreeItemReadModel)
            .ToArray();
    }

    public KnowledgeTreeItemReadModel? GetItem(string collectionId, long itemId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        return ToTreeItemReadModelOrNull(repository.GetItem(itemId));
    }

    public IReadOnlyList<KnowledgeTreeItemReadModel> LoadBreadcrumbChain(string collectionId, long itemId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        List<KnowledgeTreeItemReadModel> reversed = [];
        HashSet<long> visitedIds = [];
        KnowledgeTreeItemRow? item = repository.GetItem(itemId);

        while (item is not null && visitedIds.Add(item.Id))
        {
            reversed.Add(ToTreeItemReadModel(item));

            if (!item.ParentId.HasValue)
            {
                break;
            }

            item = repository.GetItem(item.ParentId.Value);
        }

        reversed.Reverse();
        return reversed;
    }

    public KnowledgeTreeItemReadModel? FindNoteByRelativePath(string collectionId, string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        return ToTreeItemReadModelOrNull(repository.FindNoteByFileRelPath(NormalizeRelativePath(relativePath)));
    }
}
