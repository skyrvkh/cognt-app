namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface ICreateFlashcardsCommand
{
    Task<int> ExecuteAsync(
        CreateFlashcardsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateFlashcardsRequest(
    string CollectionId,
    string DeckName,
    string Content,
    IReadOnlyList<string> Tags,
    string? SourceRelPath = null);
