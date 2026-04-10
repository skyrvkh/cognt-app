using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardAuthoringWriter
{
    Task<int> CreateCardsFromContentAsync(
        string collectionId,
        string deckName,
        string content,
        IReadOnlyList<string> tags,
        string? sourceRelPath,
        CancellationToken cancellationToken = default);

    Task<CreateCardsFromCardContextResult> CreateCardsFromCardContextAsync(
        string collectionId,
        string deckName,
        string sourceRelPath,
        CardContextSnapshot context,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default);

    Task UpdateCardFrontContentAsync(
        string collectionId,
        long cardId,
        string frontContent,
        CancellationToken cancellationToken = default);
}
