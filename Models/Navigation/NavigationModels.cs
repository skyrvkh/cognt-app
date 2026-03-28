namespace CogntApp.Models.Navigation;

public sealed record PreparedNote(string NotePath, string Title, string Content);

public sealed record OpenNoteRequestedEventArgs(string NotePath, bool SelectInTree);

public sealed record NoteSelectionRequestedEventArgs(string NotePath);

public sealed record OverlayRequestedEventArgs(OverlayViewKind ViewKind);

public sealed record UserMessageRequestedEventArgs(string Title, string Message);

public enum OverlayViewKind
{
    Dashboard,
    Decks,
}
