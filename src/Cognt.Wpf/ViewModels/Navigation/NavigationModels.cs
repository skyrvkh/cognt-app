using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Models.Navigation;

public sealed class PreparedNote
{
    public PreparedNote(
        string notePath,
        string title,
        string content,
        long? itemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);

        NotePath = notePath;
        Title = title;
        Content = content;
        ItemId = itemId;
    }

    public string NotePath { get; }

    public string Title { get; }

    public string Content { get; }

    public long? ItemId { get; }
}

public sealed class OpenNoteRequestedEventArgs
{
    public OpenNoteRequestedEventArgs(string notePath, bool SelectInTree)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notePath);

        NotePath = notePath;
        this.SelectInTree = SelectInTree;
    }

    public string NotePath { get; }

    public bool SelectInTree { get; }
}

public sealed class OpenPdfRequestedEventArgs
{
    public OpenPdfRequestedEventArgs(KnowledgePdfDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Document = document;
    }

    public KnowledgePdfDocumentReadModel Document { get; }
}

public sealed class OverlayRequestedEventArgs
{
    public OverlayRequestedEventArgs(OverlayViewKind viewKind)
    {
        ViewKind = viewKind;
    }

    public OverlayViewKind ViewKind { get; }
}

public sealed class OpenAllNotesRequestedEventArgs
{
    public OpenAllNotesRequestedEventArgs(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        RelativePath = relativePath;
    }

    public string RelativePath { get; }
}

public sealed class CollectionRemovalPlan
{
    private CollectionRemovalPlan(bool canRemove, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        CanRemove = canRemove;
        Message = message;
    }

    public bool CanRemove { get; }

    public string Message { get; }

    public static CollectionRemovalPlan Allowed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new CollectionRemovalPlan(true, message);
    }

    public static CollectionRemovalPlan Blocked(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new CollectionRemovalPlan(false, message);
    }
}

public sealed record CollectionCreationRequest(
    string Name,
    string ParentPath);

public enum OverlayViewKind
{
    Dashboard,
    AllNotes,
    Decks,
    Review,
    Settings,
}
