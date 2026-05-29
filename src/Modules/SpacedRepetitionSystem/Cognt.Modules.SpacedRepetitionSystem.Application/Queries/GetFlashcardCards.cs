using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardCards(
    IFlashcardDeckReader flashcardReader)
    : IFlashcardCardsQuery
{
    private readonly IFlashcardDeckReader reader = flashcardReader ?? throw new ArgumentNullException(nameof(flashcardReader));

    public IReadOnlyList<FlashcardCardSummaryReadModel> Execute(FlashcardCardsQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureCardsQueryRequest(request);

        int limit = request.Limit <= 0
            ? FlashcardCardsQueryRequest.DefaultLimit
            : request.Limit;

        return reader.LoadCardSummaries(
            request.CollectionId,
            request.DeckId,
            request.Filter,
            request.SourceRelPath,
            request.Tag,
            request.Tags,
            request.SearchText,
            limit);
    }
}
