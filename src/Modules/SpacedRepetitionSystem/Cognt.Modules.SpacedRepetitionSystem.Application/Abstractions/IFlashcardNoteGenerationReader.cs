using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardNoteGenerationReader
{
    IReadOnlyList<GeneratedCardContextCardReadModel> LoadGeneratedCardsForSourceNote(
        string collectionId,
        string sourceRelPath);
}
