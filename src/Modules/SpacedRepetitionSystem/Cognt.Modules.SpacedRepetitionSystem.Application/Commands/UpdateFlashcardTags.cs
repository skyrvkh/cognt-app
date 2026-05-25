using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardTags(IFlashcardAuthoringWriter flashcardWriter) : IUpdateFlashcardTagsCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task<int> AddTagsAsync(
        AddFlashcardTagsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureAddFlashcardTagsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.AddTagsToCardsAsync(
            request.CollectionId,
            request.CardIds,
            request.Tags,
            cancellationToken);
    }

    public Task<int> RemoveTagsAsync(
        RemoveFlashcardTagsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureRemoveFlashcardTagsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.RemoveTagsFromCardsAsync(
            request.CollectionId,
            request.CardIds,
            request.Tags,
            cancellationToken);
    }
}
