using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.Features.NoteToCard.ImageOcclusion;

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

    internal async void QueueMentionTargetsRefresh()
    {
        int requestId = ++mentionTargetsRefreshRequestId;

        try
        {
            IReadOnlyList<KnowledgeMentionTargetReadModel> targets = await Task.Run(viewModel.ListMentionTargets);

            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            editorView.PostMentionTargets(targets);
        }
        catch (Exception ex)
        {
            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            dialogs.ShowWarning(
                "Mentions",
                $"Mention targets could not be refreshed: {ex.Message}");
        }
    }

    internal void RefreshDocumentLinksFromCurrentContent()
    {
        RefreshDocumentLinks(editorView.GetContent());
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
            dialogs.ShowWarning("Create Cloze", ex.Message);
        }
    }

    internal async Task CreateCardContextFromSelectionAsync()
    {
        try
        {
            SelectedCardContextBlocksReadModel? selection = await editorView.GetSelectedCardContextBlocksAsync();
            if (selection is null || selection.BlockIds.Count == 0)
            {
                dialogs.ShowWarning(
                    "Card Context",
                    "Select one or more note blocks before creating a card context.");
                return;
            }

            string content = editorView.GetContent();
            viewModel.CreateCardContextFromSelection(selection.BlockIds, selection.Title, content);
            editorView.PostSaveResult(ok: true);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Create Card Context", ex.Message);
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
            dialogs.ShowWarning("Add To Card Slot", ex.Message);
        }
    }
}
