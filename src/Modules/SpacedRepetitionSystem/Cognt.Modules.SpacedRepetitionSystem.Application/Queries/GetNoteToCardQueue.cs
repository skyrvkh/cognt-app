using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetNoteToCardQueue(IFlashcardNoteGenerationReader flashcardReader) : INoteToCardQueueQuery
{
    private readonly IFlashcardNoteGenerationReader reader = flashcardReader ?? throw new ArgumentNullException(nameof(flashcardReader));

    public NoteToCardQueueReadModel Execute(NoteToCardQueueQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureNoteToCardQueueQueryRequest(request);
        IReadOnlyList<GeneratedCardContextCardReadModel> generatedCards = reader.LoadGeneratedCardsForSourceNote(
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
