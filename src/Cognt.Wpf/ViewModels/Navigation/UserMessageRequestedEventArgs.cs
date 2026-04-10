namespace CogntApp.Models.Navigation;

public sealed class UserMessageRequestedEventArgs
{
    public UserMessageRequestedEventArgs(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);

        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
