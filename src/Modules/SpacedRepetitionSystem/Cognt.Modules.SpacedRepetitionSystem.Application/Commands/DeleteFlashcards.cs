using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class DeleteFlashcards(IFlashcardAuthoringWriter flashcardWriter) : IDeleteFlashcardsCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task<int> ExecuteAsync(
        DeleteFlashcardsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureDeleteFlashcardsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.DeleteCardsAsync(
            request.CollectionId,
            request.CardIds,
            cancellationToken);
    }
}
