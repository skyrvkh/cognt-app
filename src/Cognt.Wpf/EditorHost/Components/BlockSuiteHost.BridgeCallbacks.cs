using System.Text.Json;
using CogntApp.Models.Markdown;
using Microsoft.JSInterop;
using AttachFileResult = Cognt.Wpf.EditorHost.Models.Editor.AttachFileResult;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;
using ReadAttachmentResult = Cognt.Wpf.EditorHost.Models.Editor.ReadAttachmentResult;

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
    public void HandleCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        Bridge.RaiseCreateExtract(sourcePath, selectedContent, selectedPlainText);
    }

    [JSInvokable]
    public void HandleSaveCloze(
        string sourcePath,
        string selectedPlainText,
        string clozeText,
        string content)
    {
        Bridge.RaiseSaveCloze(sourcePath, selectedPlainText, clozeText, content);
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

    private static object CreateMentionTargetPayload(MentionTarget target)
    {
        return new
        {
            relativePath = target.RelativePath,
            title = target.Title,
            fileName = target.FileName,
            recentRank = target.RecentRank,
        };
    }
}
