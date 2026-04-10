using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IRecordFlashcardReviewCommand
{
    Task ExecuteAsync(
        RecordFlashcardReviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RecordFlashcardReviewRequest(
    string CollectionId,
    long CardId,
    FlashcardReviewRatingReadModel Rating);
