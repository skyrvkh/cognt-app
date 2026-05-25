namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IDeleteFlashcardsCommand
{
    Task<int> ExecuteAsync(
        DeleteFlashcardsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DeleteFlashcardsRequest(
    string CollectionId,
    IReadOnlyList<long> CardIds);
