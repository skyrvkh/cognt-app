using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardCardsQuery
{
    IReadOnlyList<FlashcardCardSummaryReadModel> Execute(FlashcardCardsQueryRequest request);
}

public enum FlashcardCardsFilterKind
{
    All = 0,
    Due = 1,
    New = 2,
    LearningOrRelearning = 3,
    ReviewedToday = 4,
    Suspended = 5,
}

public sealed record FlashcardCardsQueryRequest(
    string CollectionId,
    long? DeckId = null,
    FlashcardCardsFilterKind Filter = FlashcardCardsFilterKind.All,
    string? SourceRelPath = null,
    string? Tag = null,
    IReadOnlyList<string>? Tags = null,
    int Limit = FlashcardCardsQueryRequest.DefaultLimit)
{
    public const int DefaultLimit = 500;
}
