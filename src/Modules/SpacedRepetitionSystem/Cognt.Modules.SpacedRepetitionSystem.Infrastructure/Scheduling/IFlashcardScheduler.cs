using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal interface IFlashcardScheduler
{
    Task<FlashcardSchedule> CreateNewCardAsync(CancellationToken cancellationToken = default);

    Task<FlashcardSchedule> ReviewCardAsync(
        string serializedState,
        FlashcardReviewRatingReadModel rating,
        double desiredRetention,
        CancellationToken cancellationToken = default);
}
