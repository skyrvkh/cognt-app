namespace CogntApp.ViewModels.Navigation;

internal sealed class UserMessageRequestedEventArgs
{
    internal UserMessageRequestedEventArgs(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);

        Title = title;
        Message = message;
    }

    internal string Title { get; }

    internal string Message { get; }
}
