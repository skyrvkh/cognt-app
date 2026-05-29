namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    private static TRequest EnsureRequest<TRequest>(TRequest? request)
        where TRequest : class
    {
        return request ?? throw new ArgumentNullException(nameof(request));
    }
}
