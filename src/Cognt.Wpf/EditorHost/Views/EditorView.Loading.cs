using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public async Task LoadNoteAsync(
        string title,
        string content,
        string? path = null,
        string? collectionRoot = null)
    {
        int requestId = BeginNoteLoadRequest(title);

        await WarmUpAsync();

        if (!IsCurrentLoadRequest(requestId))
        {
            ClearTransientEditorState();
            return;
        }

        SetCurrentNoteLocation(path, collectionRoot);

        try
        {
            await LoadNoteIntoHostAsync(
                path,
                title,
                content,
                collectionRoot,
                "The editor did not finish loading the note.");
        }
        catch (Exception ex)
        {
            if (isDisposingEditorHost || suppressBridgeCommands)
            {
                logger.LogDebug(ex, "EditorView LoadNote stopped during host disposal. Path={Path} RequestId={RequestId}", path, requestId);
            }
            else
            {
                logger.LogError(ex, "EditorView LoadNote failed. Path={Path} RequestId={RequestId}", path, requestId);
            }

            if (IsCurrentLoadRequest(requestId))
            {
                ClearCurrentNoteLocation();
            }

            throw;
        }

        if (!IsCurrentLoadRequest(requestId))
        {
            return;
        }

        CompleteNoteLoad(content);
        await FlushMentionTargetsAsync();
    }

    private Task LoadNoteIntoHostAsync(
        string? path,
        string title,
        string content,
        string? collectionRoot,
        string timeoutMessage)
    {
        return WaitWithTimeoutAsync(
            blockSuiteBridge.LoadNoteAsync(path, title, content, collectionRoot),
            GetDocumentLoadTimeout(content),
            timeoutMessage);
    }

    private Task ReloadCurrentNoteIntoHostAsync(string timeoutMessage)
    {
        return LoadNoteIntoHostAsync(
            currentSourcePath,
            currentTitle,
            currentContent,
            currentCollectionRoot,
            timeoutMessage);
    }
}
