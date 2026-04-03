namespace CogntApp.Models.Navigation;

public sealed record PreparedNote(
    string NotePath,
    string Title,
    string Content,
    long? ItemId = null);

public sealed record OpenNoteRequestedEventArgs(string NotePath, bool SelectInTree);

public sealed record OverlayRequestedEventArgs(OverlayViewKind ViewKind);

public sealed record UserMessageRequestedEventArgs(string Title, string Message);

public enum OverlayViewKind
{
    Dashboard,
    Decks,
    Review,
}
