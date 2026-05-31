using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class DeleteFlashcardDeck(
    IFlashcardAuthoringWriter flashcardWriter)
    : IDeleteFlashcardDeckCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public async Task<int> ExecuteAsync(
        DeleteFlashcardDeckRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureDeleteFlashcardDeckRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        int deletedCount = await writer.DeleteDecksAsync(
            request.CollectionId,
            request.DeckIds,
            cancellationToken).ConfigureAwait(false);
        return deletedCount;
    }
}
