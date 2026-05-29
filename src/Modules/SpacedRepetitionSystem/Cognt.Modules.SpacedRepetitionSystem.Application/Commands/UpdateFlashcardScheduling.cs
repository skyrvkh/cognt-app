using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardScheduling(
    IFlashcardReviewWriter flashcardWriter)
    : IUpdateFlashcardSchedulingCommand
{
    private readonly IFlashcardReviewWriter writer = flashcardWriter ?? throw new ArgumentNullException(nameof(flashcardWriter));

    public async Task SetSuspensionAsync(
        UpdateFlashcardSuspensionRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateSuspensionRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await writer.SetCardSuspensionAsync(
            request.CollectionId,
            request.CardId,
            request.IsSuspended,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task PostponeDueDateAsync(
        PostponeFlashcardDueDateRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsurePostponeDueDateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await writer.PostponeCardDueDateAsync(
            request.CollectionId,
            request.CardId,
            request.Days,
            cancellationToken).ConfigureAwait(false);
    }
}
