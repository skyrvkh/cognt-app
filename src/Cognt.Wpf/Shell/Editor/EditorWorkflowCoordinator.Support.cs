using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed partial class EditorWorkflowCoordinator
{
    internal void AttachBlockSuiteHostToEditor()
    {
        editorView.AttachBlockSuiteHostToEditor();
    }

    internal Task WarmUpAsync()
    {
        return editorView.WarmUpAsync();
    }

    internal void UpdateCurrentNoteTitle(string title)
    {
        editorView.UpdateCurrentNoteTitle(title);
    }

    internal Task SetReadOnlyAsync(bool isReadOnly)
    {
        return editorView.SetReadOnlyAsync(isReadOnly);
    }

    private async void EditorView_RetryRequested(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(retryNotePath))
        {
            return;
        }

        await OpenNoteAsync(retryNotePath, retrySelectInTree);
    }

    internal void QueueMentionTargetsRefresh()
    {
        refreshes.QueueMentionTargetsRefresh();
    }

    internal void RefreshDocumentLinksFromCurrentContent()
    {
        refreshes.RefreshDocumentLinksFromCurrentContent();
    }

    internal async Task OpenImageOcclusionForSelectedImageAsync()
    {
        try
        {
            SelectedImageBlockReadModel? selectedImage = await editorView.GetSelectedImageBlockAsync();
            if (selectedImage is null
                || string.IsNullOrWhiteSpace(selectedImage.ImageBlockId)
                || string.IsNullOrWhiteSpace(selectedImage.ImageRelPath))
            {
                dialogs.ShowWarning(
                    "Image Occlusion",
                    "Select an image block in the note before opening the image occlusion editor.");
                return;
            }

            string content = editorView.GetContent();
            await OpenImageOcclusionForImageAsync(
                selectedImage.ImageBlockId,
                selectedImage.ImageRelPath,
                content);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Open image occlusion for selected image failed.");
            dialogs.ShowWarning("Image Occlusion", ex.Message);
        }
    }

    internal async Task OpenImageOcclusionForImageAsync(
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        try
        {
            OpenImageMaskEditorRequest request = viewModel.CreateImageMaskEditorRequest(
                imageBlockId,
                imageRelPath,
                content);
            ImageMaskEditorResult result = await imageMaskEditorDialogs.ShowAsync(
                request,
                async applied =>
                {
                    viewModel.SaveImageMaskEditorResult(applied, content);
                    editorView.PostSaveResult(ok: true);
                    await Task.CompletedTask;
                });
            if (!result.Saved)
            {
                return;
            }

            viewModel.SaveImageMaskEditorResult(result, content);
            editorView.PostSaveResult(ok: true);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Open image occlusion failed. ImageBlockId={ImageBlockId} ImageRelPath={ImageRelPath}", imageBlockId, imageRelPath);
            dialogs.ShowWarning("Image Occlusion", ex.Message);
        }
    }

    internal async Task CreateClozeFromSelectionAsync()
    {
        try
        {
            await editorView.CreateClozeFromSelectionAsync();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Create cloze from selection failed.");
            dialogs.ShowWarning("Create Cloze", ex.Message);
        }
    }

    internal async Task AddSelectedTextToSelectedCardSlotAsync()
    {
        try
        {
            CardCandidateReadModel? candidate = viewModel.SelectedCardCandidate;
            if (candidate is null)
            {
                dialogs.ShowWarning("Note Cards", "Select a card candidate first.");
                return;
            }

            await editorView.CreateClozeFromSelectionAsync(
                candidate.ContextId,
                candidate.SlotId,
                candidate.DisplayClozeIndex);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Add selected text to selected card slot failed.");
            dialogs.ShowWarning("Add To Card Slot", ex.Message);
        }
    }
}
