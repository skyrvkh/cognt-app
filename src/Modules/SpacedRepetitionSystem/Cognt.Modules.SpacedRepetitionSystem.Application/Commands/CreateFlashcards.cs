using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class CreateFlashcards(
    IFlashcardAuthoringWriter flashcardWriter)
    : ICreateFlashcardsCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public async Task<int> ExecuteAsync(
        CreateFlashcardsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureCreateFlashcardsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        string[] tags = request.Tags.ToArray();
        int createdCount = await writer.CreateCardsFromContentAsync(
            request.CollectionId,
            request.DeckName,
            request.Content,
            tags,
            request.SourceRelPath,
            cancellationToken).ConfigureAwait(false);
        return createdCount;
    }
}
