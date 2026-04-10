using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardDeckSummariesQuery
{
    IReadOnlyList<FlashcardDeckSummaryReadModel> Execute(FlashcardCollectionQueryRequest request);
}

public sealed record FlashcardCollectionQueryRequest(
    string CollectionId);
