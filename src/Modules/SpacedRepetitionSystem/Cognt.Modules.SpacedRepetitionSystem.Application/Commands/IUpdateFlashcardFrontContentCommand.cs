namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IUpdateFlashcardFrontContentCommand
{
    Task ExecuteAsync(
        UpdateFlashcardFrontContentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateFlashcardFrontContentRequest(
    string CollectionId,
    long CardId,
    string FrontContent);
