using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class CreateFlashcards(IFlashcardAuthoringWriter flashcardWriter) : ICreateFlashcardsCommand
{
    private readonly IFlashcardAuthoringWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task<int> ExecuteAsync(
        CreateFlashcardsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureCreateFlashcardsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        string[] tags = request.Tags.ToArray();

        return writer.CreateCardsFromContentAsync(
            request.CollectionId,
            request.DeckName,
            request.Content,
            tags,
            request.SourceRelPath,
            cancellationToken);
    }
}
