using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardDeckReader
{
    IReadOnlyList<FlashcardDeckSummaryReadModel> LoadDeckSummaries(string collectionId);

    IReadOnlyList<FlashcardCardSummaryReadModel> LoadCardSummaries(
        string collectionId,
        long? deckId = null,
        FlashcardCardsFilterKind filter = FlashcardCardsFilterKind.All,
        string? sourceRelPath = null,
        string? tag = null,
        int limit = 500);
}
