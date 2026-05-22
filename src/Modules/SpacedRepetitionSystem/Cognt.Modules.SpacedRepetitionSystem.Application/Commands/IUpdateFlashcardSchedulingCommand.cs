namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IUpdateFlashcardSchedulingCommand
{
    Task SetSuspensionAsync(
        UpdateFlashcardSuspensionRequest request,
        CancellationToken cancellationToken = default);

    Task PostponeDueDateAsync(
        PostponeFlashcardDueDateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateFlashcardSuspensionRequest(
    string CollectionId,
    long CardId,
    bool IsSuspended);

public sealed record PostponeFlashcardDueDateRequest(
    string CollectionId,
    long CardId,
    int Days);
