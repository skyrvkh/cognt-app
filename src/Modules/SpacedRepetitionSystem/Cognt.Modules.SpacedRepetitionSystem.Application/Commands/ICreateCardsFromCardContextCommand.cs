using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface ICreateCardsFromCardContextCommand
{
    Task<CreateCardsFromCardContextResult> ExecuteAsync(
        CreateCardsFromCardContextRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateCardsFromCardContextRequest(
    string CollectionId,
    string DeckName,
    string SourceRelPath,
    CardContextSnapshot Context,
    IReadOnlyList<string> Tags);

public sealed record CreateCardsFromCardContextResult(
    int CreatedCards,
    int UpdatedCards,
    IReadOnlyList<long> AffectedCardIds,
    IReadOnlyList<string> OrphanedSlotIds);
