using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetNoteToCardQueue(IFlashcardNoteGenerationReader flashcardPort) : INoteToCardQueueQuery
{
    private readonly IFlashcardNoteGenerationReader port = flashcardPort ?? throw new ArgumentNullException(nameof(flashcardPort));

    public NoteToCardQueueReadModel Execute(NoteToCardQueueQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureNoteToCardQueueQueryRequest(request);
        IReadOnlyList<GeneratedCardContextCardReadModel> generatedCards = port.LoadGeneratedCardsForSourceNote(
            request.CollectionId,
            request.SourceRelPath);

        return CardContextQueueBuilder.Build(
            request.CollectionId,
            request.SourceRelPath,
            request.NoteContent,
            request.DraftDocument,
            generatedCards);
    }
}
