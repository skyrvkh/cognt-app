using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardDashboardSummaryQuery
{
    FlashcardDashboardSummaryReadModel Execute(
        FlashcardCollectionQueryRequest request,
        IReadOnlyList<string>? deckNames = null);
}
