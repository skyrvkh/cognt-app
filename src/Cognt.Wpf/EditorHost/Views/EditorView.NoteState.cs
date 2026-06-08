namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private int BeginNoteLoadRequest(string title)
    {
        ClearTransientEditorState();
        int requestId = ++loadRequestId;
        ShowLoadingState("Loading note", "Opening the selected note...");
        ResetCurrentNoteState(title);
        return requestId;
    }

    private void CancelOutstandingLoadRequests()
    {
        ++loadRequestId;
    }

    private bool IsCurrentLoadRequest(int requestId)
    {
        return requestId == loadRequestId;
    }

    private void ResetCurrentNoteState(string title = "")
    {
        currentTitle = title;
        currentContent = string.Empty;
        ClearCurrentNoteLocation();
    }

    private void SetCurrentNoteLocation(string? path, string? collectionRoot)
    {
        currentSourcePath = path;
        currentCollectionRoot = collectionRoot;
    }

    private void ClearCurrentNoteLocation()
    {
        currentSourcePath = null;
        currentCollectionRoot = null;
    }

    private void CompleteNoteLoad(string content)
    {
        currentContent = content;
        needsHostRestore = false;
        ShowReadyState();
    }
}
