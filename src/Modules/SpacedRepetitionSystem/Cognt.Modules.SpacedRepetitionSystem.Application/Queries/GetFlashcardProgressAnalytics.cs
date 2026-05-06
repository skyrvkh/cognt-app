using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardProgressAnalytics(IFlashcardDashboardReader flashcardReader) : IFlashcardProgressAnalyticsQuery
{
    private readonly IFlashcardDashboardReader reader = flashcardReader ?? throw new ArgumentNullException(nameof(flashcardReader));

    public FlashcardProgressAnalyticsReadModel Execute(FlashcardProgressAnalyticsQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureProgressAnalyticsQueryRequest(request);

        return reader.LoadProgressAnalytics(request.CollectionId, request.Today, request.DeckNames);
    }
}
