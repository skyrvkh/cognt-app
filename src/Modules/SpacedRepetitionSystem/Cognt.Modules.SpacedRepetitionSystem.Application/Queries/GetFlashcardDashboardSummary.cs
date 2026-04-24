using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardDashboardSummary(IFlashcardDashboardReader flashcardReader) : IFlashcardDashboardSummaryQuery
{
    private readonly IFlashcardDashboardReader reader = flashcardReader ?? throw new ArgumentNullException(nameof(flashcardReader));

    public FlashcardDashboardSummaryReadModel Execute(
        FlashcardCollectionQueryRequest request,
        IReadOnlyList<string>? deckNames = null)
    {
        request = FlashcardRequestGuards.EnsureCollectionQueryRequest(request);

        return reader.LoadDashboardSummary(request.CollectionId, NormalizeDeckNames(deckNames));
    }

    private static IReadOnlyList<string> NormalizeDeckNames(IReadOnlyList<string>? deckNames)
    {
        return deckNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
