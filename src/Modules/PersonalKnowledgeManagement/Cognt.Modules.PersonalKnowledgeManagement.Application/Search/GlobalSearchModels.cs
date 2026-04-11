namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Search;

public sealed record GlobalSearchCollectionScope(
    string CollectionId,
    string CollectionName);

public sealed record GlobalSearchRequest(
    string Query,
    IReadOnlyList<GlobalSearchCollectionScope> Collections,
    int Limit = 50);

public sealed record GlobalSearchResultReadModel(
    string Title,
    string Snippet,
    string RelativePath,
    string CollectionId,
    string CollectionName);
