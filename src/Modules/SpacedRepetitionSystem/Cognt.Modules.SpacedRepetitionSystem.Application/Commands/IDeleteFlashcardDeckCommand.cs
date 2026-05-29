namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IDeleteFlashcardDeckCommand
{
    Task<int> ExecuteAsync(
        DeleteFlashcardDeckRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DeleteFlashcardDeckRequest(
    string CollectionId,
    IReadOnlyList<long> DeckIds);
