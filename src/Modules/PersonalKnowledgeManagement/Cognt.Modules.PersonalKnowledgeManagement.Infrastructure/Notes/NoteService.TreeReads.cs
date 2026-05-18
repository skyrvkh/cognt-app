using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    IReadOnlyList<KnowledgeTreeItemReadModel> INoteService.LoadRootItems(string collectionId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        EnsureAllPdfAttachmentTreeItems(repository, preserveDeletedTreeItems: true);

        return repository.LoadRootItems()
            .Select(ToTreeItemReadModel)
            .ToArray();
    }

    IReadOnlyList<KnowledgeTreeItemReadModel> INoteService.LoadChildren(string collectionId, long parentId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        EnsureNotePdfAttachmentTreeItems(repository, parentId, preserveDeletedTreeItems: true);

        return repository.LoadChildren(parentId)
            .Select(ToTreeItemReadModel)
            .ToArray();
    }

    KnowledgeTreeItemReadModel? INoteService.GetItem(string collectionId, long itemId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        return ToTreeItemReadModelOrNull(repository.GetItem(itemId));
    }

    IReadOnlyList<KnowledgeTreeItemReadModel> INoteService.LoadBreadcrumbChain(string collectionId, long itemId)
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

    KnowledgeTreeItemReadModel? INoteService.FindNoteByRelativePath(string collectionId, string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        return ToTreeItemReadModelOrNull(repository.FindNoteByFileRelPath(NormalizeRelativePath(relativePath)));
    }
}
