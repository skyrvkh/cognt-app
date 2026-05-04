using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class CreateCardsFromCardContext(IFlashcardAuthoringWriter flashcardWriter)
    : ICreateCardsFromCardContextCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task<CreateCardsFromCardContextResult> ExecuteAsync(
        CreateCardsFromCardContextRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureCreateCardsFromCardContextRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.CreateCardsFromCardContextAsync(
            request.CollectionId,
            request.DeckName,
            request.SourceRelPath,
            request.Context,
            request.Tags.ToArray(),
            cancellationToken);
    }
}
