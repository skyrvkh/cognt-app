using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardDeckSummaries(
    IFlashcardDeckReader flashcardReader)
    : IFlashcardDeckSummariesQuery
{
    private readonly IFlashcardDeckReader reader = flashcardReader ?? throw new ArgumentNullException(nameof(flashcardReader));

    public IReadOnlyList<FlashcardDeckSummaryReadModel> Execute(FlashcardCollectionQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureCollectionQueryRequest(request);

        return reader.LoadDeckSummaries(request.CollectionId);
    }
}
