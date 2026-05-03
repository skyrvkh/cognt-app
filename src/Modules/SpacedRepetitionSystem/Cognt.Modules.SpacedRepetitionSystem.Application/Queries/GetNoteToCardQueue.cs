using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetNoteToCardQueue(IFlashcardNoteGenerationReader flashcardNotes) : INoteToCardQueueQuery
{
    private readonly IFlashcardNoteGenerationReader notes = flashcardNotes ?? throw new ArgumentNullException(nameof(flashcardNotes));

    public NoteToCardQueueReadModel Execute(NoteToCardQueueQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureNoteToCardQueueQueryRequest(request);
        IReadOnlyList<GeneratedCardContextCardReadModel> generatedCards = notes.LoadGeneratedCardsForSourceNote(
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
