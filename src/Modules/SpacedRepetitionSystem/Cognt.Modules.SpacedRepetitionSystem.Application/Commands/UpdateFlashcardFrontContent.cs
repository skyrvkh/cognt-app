using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardFrontContent(
    IFlashcardAuthoringWriter flashcardWriter)
    : IUpdateFlashcardFrontContentCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public async Task ExecuteAsync(
        UpdateFlashcardFrontContentRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateFrontContentRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await writer.UpdateCardFrontContentAsync(
            request.CollectionId,
            request.CardId,
            request.FrontContent,
            cancellationToken).ConfigureAwait(false);
    }
}
