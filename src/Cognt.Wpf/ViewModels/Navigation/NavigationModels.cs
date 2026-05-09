using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels.Navigation;

internal sealed class PreparedNote
{
    internal PreparedNote(
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

    internal string NotePath { get; }

    internal string Title { get; }

    internal string Content { get; }

    internal long? ItemId { get; }
}

internal sealed class OpenNoteRequestedEventArgs
{
    internal OpenNoteRequestedEventArgs(string notePath, bool SelectInTree)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notePath);

        NotePath = notePath;
        this.SelectInTree = SelectInTree;
    }

    internal string NotePath { get; }

    internal bool SelectInTree { get; }
}

internal sealed class OpenPdfRequestedEventArgs
{
    internal OpenPdfRequestedEventArgs(KnowledgePdfDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Document = document;
    }

    internal KnowledgePdfDocumentReadModel Document { get; }
}

internal sealed class OverlayRequestedEventArgs
{
    internal OverlayRequestedEventArgs(OverlayViewKind viewKind)
    {
        ViewKind = viewKind;
    }

    internal OverlayViewKind ViewKind { get; }
}

internal sealed class OpenAllNotesRequestedEventArgs
{
    internal OpenAllNotesRequestedEventArgs(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        RelativePath = relativePath;
    }

    internal string RelativePath { get; }
}

internal sealed class CollectionRemovalPlan
{
    private CollectionRemovalPlan(bool canRemove, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        CanRemove = canRemove;
        Message = message;
    }

    internal bool CanRemove { get; }

    internal string Message { get; }

    internal static CollectionRemovalPlan Allowed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new CollectionRemovalPlan(true, message);
    }

    internal static CollectionRemovalPlan Blocked(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new CollectionRemovalPlan(false, message);
    }
}

internal sealed record CollectionCreationRequest(
    string Name,
    string ParentPath);

internal enum OverlayViewKind
{
    Dashboard,
    AllNotes,
    Decks,
    Review,
    Settings,
}
