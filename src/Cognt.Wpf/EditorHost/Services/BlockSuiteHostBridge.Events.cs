using System.Text.Json;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace Cognt.Wpf.EditorHost.Services;

public sealed partial class BlockSuiteHostBridge
{
    internal event EventHandler? DocumentChanged;
    internal event EventHandler<string>? ContentChanged;
    internal event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    internal event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    internal event EventHandler<OpenImageOcclusionRequestedEventArgs>? OpenImageOcclusionRequested;
    internal event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    internal event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    internal event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    internal event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    internal event Func<object?, AttachFileRequestedEventArgs, Task>? AttachFileRequested;
    internal event Func<object?, ReadAttachmentRequestedEventArgs, Task>? ReadAttachmentRequested;
    internal event EventHandler<string>? DiagnosticReceived;

    internal void RaiseContentChanged(string content)
    {
        ContentChanged?.Invoke(this, content);
    }

    internal void RaiseDocumentChanged()
    {
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        CreateExtractRequested?.Invoke(
            this,
            new CreateExtractRequestedEventArgs(sourcePath, selectedContent, selectedPlainText));
    }

    internal void RaiseSaveCloze(
        string sourcePath,
        string blockId,
        string selectedPlainText,
        string clozeText,
        string content,
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        SaveClozeRequested?.Invoke(
            this,
            new SaveClozeRequestedEventArgs(
                sourcePath,
                blockId,
                selectedPlainText,
                clozeText,
                content,
                cardContextId,
                slotId,
                displayClozeIndex));
    }

    internal void RaiseOpenImageOcclusion(
        string sourcePath,
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        OpenImageOcclusionRequested?.Invoke(
            this,
            new OpenImageOcclusionRequestedEventArgs(sourcePath, imageBlockId, imageRelPath, content));
    }

    internal void RaiseMentionPreview(
        string requestId,
        string sourcePath,
        string relativePath,
        string mode,
        int maxCharacters)
    {
        MentionPreviewRequested?.Invoke(
            this,
            new MentionPreviewRequestedEventArgs(requestId, sourcePath, relativePath, mode, maxCharacters));
    }

    internal IReadOnlyList<KnowledgeMentionTargetReadModel> RaiseMentionSearch(string sourcePath, string query, int limit)
    {
        MentionSearchRequestedEventArgs args = new(sourcePath, query, limit);
        MentionSearchRequested?.Invoke(this, args);
        return args.Targets;
    }

    internal void RaiseOpenMention(string sourcePath, string relativePath, string content)
    {
        OpenMentionRequested?.Invoke(this, new OpenMentionRequestedEventArgs(sourcePath, relativePath, content));
    }

    internal CreateLinkedNoteResult RaiseCreateLinkedNote(string sourcePath, string viewMode)
    {
        CreateLinkedNoteRequestedEventArgs args = new(sourcePath, viewMode);
        CreateLinkedNoteRequested?.Invoke(this, args);
        return args.Result;
    }

    internal async Task<AttachFileResult> RaiseAttachFileAsync(
        string sourcePath,
        string fileName,
        string contentBase64)
    {
        AttachFileRequestedEventArgs args = new(sourcePath, fileName, contentBase64);
        if (AttachFileRequested is null)
        {
            return args.Result;
        }

        foreach (Delegate handler in AttachFileRequested.GetInvocationList())
        {
            if (handler is Func<object?, AttachFileRequestedEventArgs, Task> attachHandler)
            {
                await attachHandler(this, args);
            }
        }

        return args.Result;
    }

    internal async Task<ReadAttachmentResult> RaiseReadAttachmentAsync(
        string sourcePath,
        string imageSource)
    {
        ReadAttachmentRequestedEventArgs args = new(sourcePath, imageSource);
        if (ReadAttachmentRequested is null)
        {
            return args.Result;
        }

        foreach (Delegate handler in ReadAttachmentRequested.GetInvocationList())
        {
            if (handler is Func<object?, ReadAttachmentRequestedEventArgs, Task> readHandler)
            {
                await readHandler(this, args);
            }
        }

        return args.Result;
    }

    internal void RaiseHostMessage(JsonElement message)
    {
        if (!message.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.GetString() != DiagnosticMessageType
            || !message.TryGetProperty("message", out JsonElement messageElement))
        {
            return;
        }

        DiagnosticReceived?.Invoke(this, messageElement.GetString() ?? string.Empty);
    }
}
