namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IUpdateFlashcardTagsCommand
{
    Task<int> AddTagsAsync(
        AddFlashcardTagsRequest request,
        CancellationToken cancellationToken = default);

    Task<int> RemoveTagsAsync(
        RemoveFlashcardTagsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AddFlashcardTagsRequest(
    string CollectionId,
    IReadOnlyList<long> CardIds,
    IReadOnlyList<string> Tags);

public sealed record RemoveFlashcardTagsRequest(
    string CollectionId,
    IReadOnlyList<long> CardIds,
    IReadOnlyList<string> Tags);
