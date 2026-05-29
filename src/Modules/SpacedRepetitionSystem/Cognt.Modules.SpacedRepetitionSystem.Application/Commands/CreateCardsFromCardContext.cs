using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class CreateCardsFromCardContext(
    IFlashcardAuthoringWriter flashcardWriter)
    : ICreateCardsFromCardContextCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public async Task<CreateCardsFromCardContextResult> ExecuteAsync(
        CreateCardsFromCardContextRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureCreateCardsFromCardContextRequest(request);
        cancellationToken.ThrowIfCancellationRequested();
        string[] tags = request.Tags.ToArray();

        CreateCardsFromCardContextResult result = await writer.CreateCardsFromCardContextAsync(
            request.CollectionId,
            request.DeckName,
            request.SourceRelPath,
            request.Context,
            tags,
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}
