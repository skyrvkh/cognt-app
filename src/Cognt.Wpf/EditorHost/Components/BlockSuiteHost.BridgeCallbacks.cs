using System.Text.Json;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.JSInterop;

namespace Cognt.Wpf.EditorHost.Components;

public partial class BlockSuiteHost
{
    [JSInvokable]
    public IReadOnlyList<object> HandleSearchMentionTargets(string sourcePath, string query, int limit)
    {
        return Bridge.RaiseMentionSearch(sourcePath, query, limit)
            .Select(CreateMentionTargetPayload)
            .ToArray();
    }

    [JSInvokable]
    public void HandleContentChanged(string content)
    {
        Bridge.RaiseContentChanged(content);
    }

    [JSInvokable]
    public void HandleDocumentChanged()
    {
        Bridge.RaiseDocumentChanged();
    }

    [JSInvokable]
    public void HandleCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        Bridge.RaiseCreateExtract(sourcePath, selectedContent, selectedPlainText);
    }

    [JSInvokable]
    public void HandleSaveCloze(
        string sourcePath,
        string blockId,
        string selectedPlainText,
        string clozeText,
        string content,
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        Bridge.RaiseSaveCloze(
            sourcePath,
            blockId,
            selectedPlainText,
            clozeText,
            content,
            cardContextId,
            slotId,
            displayClozeIndex);
    }

    [JSInvokable]
    public void HandleOpenImageOcclusion(
        string sourcePath,
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        Bridge.RaiseOpenImageOcclusion(sourcePath, imageBlockId, imageRelPath, content);
    }

    [JSInvokable]
    public void HandleRequestMentionPreview(
        string requestId,
        string sourcePath,
        string relativePath,
        string mode,
        int maxCharacters)
    {
        Bridge.RaiseMentionPreview(requestId, sourcePath, relativePath, mode, maxCharacters);
    }

    [JSInvokable]
    public void HandleOpenMention(string sourcePath, string relativePath, string content)
    {
        Bridge.RaiseOpenMention(sourcePath, relativePath, content);
    }

    [JSInvokable]
    public CreateLinkedNoteResult HandleCreateLinkedNote(string sourcePath, string viewMode)
    {
        return Bridge.RaiseCreateLinkedNote(sourcePath, viewMode);
    }

    [JSInvokable]
    public Task<AttachFileResult> HandleAttachFile(
        string sourcePath,
        string fileName,
        string contentBase64)
    {
        return Bridge.RaiseAttachFileAsync(sourcePath, fileName, contentBase64);
    }

    [JSInvokable]
    public Task<ReadAttachmentResult> HandleReadAttachment(
        string sourcePath,
        string imageSource)
    {
        return Bridge.RaiseReadAttachmentAsync(sourcePath, imageSource);
    }

    [JSInvokable]
    public void HandleHostMessage(JsonElement message)
    {
        Bridge.RaiseHostMessage(message);
    }

    private static object CreateMentionTargetPayload(KnowledgeMentionTargetReadModel target)
    {
        return new
        {
            relativePath = target.RelativePath,
            title = target.Title,
        };
    }

    private static object CreateDocumentLinkPayload(KnowledgeDocumentLinkReadModel link)
    {
        return new
        {
            openRelativePath = link.OpenRelativePath,
            displayTitle = link.DisplayTitle,
            snippet = link.Snippet,
            matchedText = link.MatchedText,
            updatedAt = link.UpdatedAt,
        };
    }
}
