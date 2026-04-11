namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Search;

public interface IGlobalSearchService
{
    IReadOnlyList<GlobalSearchResultReadModel> Search(
        GlobalSearchRequest request,
        CancellationToken cancellationToken = default);
}
