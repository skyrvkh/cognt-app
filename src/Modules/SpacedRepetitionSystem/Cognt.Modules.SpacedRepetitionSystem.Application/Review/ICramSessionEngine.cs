using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public interface ICramSessionEngine
{
    CramSessionState Start(
        IReadOnlyList<long> cardIds,
        CrammingSettings settings);

    CramSessionState Start(
        IReadOnlyList<long> cardIds,
        CrammingSettings settings,
        int cardsSelected,
        int cardsSkippedInvalid);

    CramRatingResult RateCurrent(
        CramSessionState state,
        FlashcardReviewRatingReadModel rating,
        DateTimeOffset? ratedAt = null);
}
