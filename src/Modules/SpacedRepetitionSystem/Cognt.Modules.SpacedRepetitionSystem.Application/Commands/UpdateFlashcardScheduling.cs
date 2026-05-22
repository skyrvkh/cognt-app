using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardScheduling(IFlashcardReviewWriter flashcardWriter) : IUpdateFlashcardSchedulingCommand
{
    private readonly IFlashcardReviewWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public Task SetSuspensionAsync(
        UpdateFlashcardSuspensionRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateSuspensionRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.SetCardSuspensionAsync(
            request.CollectionId,
            request.CardId,
            request.IsSuspended,
            cancellationToken);
    }

    public Task PostponeDueDateAsync(
        PostponeFlashcardDueDateRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsurePostponeDueDateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.PostponeCardDueDateAsync(
            request.CollectionId,
            request.CardId,
            request.Days,
            cancellationToken);
    }
}
