using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public interface IAllNotesCatalogSource
{
    IReadOnlyList<KnowledgeTreeItemReadModel> LoadRootItems(string collectionId);

    IReadOnlyList<KnowledgeTreeItemReadModel> LoadChildren(string collectionId, long parentId);

    IReadOnlySet<string> SearchNoteCatalog(string collectionId, string query);

    IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(string collectionId);
}
