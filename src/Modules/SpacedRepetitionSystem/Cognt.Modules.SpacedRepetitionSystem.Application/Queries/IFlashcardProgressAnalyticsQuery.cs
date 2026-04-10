using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardProgressAnalyticsQuery
{
    FlashcardProgressAnalyticsReadModel Execute(FlashcardProgressAnalyticsQueryRequest request);
}

public sealed record FlashcardProgressAnalyticsQueryRequest(
    string CollectionId,
    DateOnly Today,
    IReadOnlyList<string>? DeckNames = null);
