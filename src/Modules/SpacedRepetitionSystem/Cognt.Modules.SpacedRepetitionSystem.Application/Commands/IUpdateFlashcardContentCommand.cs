namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IUpdateFlashcardContentCommand
{
    Task ExecuteAsync(
        UpdateFlashcardContentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateFlashcardContentRequest(
    string CollectionId,
    long CardId,
    string FrontContent,
    string? BackContent,
    string DeckName,
    IReadOnlyList<string> Tags);
