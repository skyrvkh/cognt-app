using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardContent(IFlashcardAuthoringWriter flashcardWriter) : IUpdateFlashcardContentCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task ExecuteAsync(
        UpdateFlashcardContentRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateContentRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.UpdateCardContentAsync(
            request.CollectionId,
            request.CardId,
            request.FrontContent,
            request.BackContent,
            request.DeckName,
            request.Tags,
            cancellationToken);
    }
}
