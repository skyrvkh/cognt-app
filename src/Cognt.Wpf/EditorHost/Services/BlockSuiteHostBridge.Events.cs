using System.IO;
using System.Text.Json;
using CogntApp.Models.Markdown;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CreateExtractRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateExtractRequestedEventArgs;
using CreateLinkedNoteRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteRequestedEventArgs;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;
using MentionPreviewRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionPreviewRequestedEventArgs;
using MentionSearchRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionSearchRequestedEventArgs;
using OpenMentionRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.OpenMentionRequestedEventArgs;
using SaveClozeRequestedEventArgs = CogntApp.Models.Editor.SaveClozeRequestedEventArgs;

namespace Cognt.Wpf.EditorHost.Services;

public sealed partial class BlockSuiteHostBridge
{
    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    public event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    public event EventHandler<string>? DiagnosticReceived;

    internal void RaiseContentChanged(string content)
    {
        ContentChanged?.Invoke(this, content);
    }

    internal void RaiseCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        CreateExtractRequested?.Invoke(
            this,
            new CreateExtractRequestedEventArgs(sourcePath, selectedContent, selectedPlainText));
    }

    internal void RaiseSaveCloze(string sourcePath, string selectedPlainText, string clozeText, string content)
    {
        SaveClozeRequested?.Invoke(
            this,
            new SaveClozeRequestedEventArgs(sourcePath, selectedPlainText, clozeText, content));
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

    internal IReadOnlyList<MentionTarget> RaiseMentionSearch(string sourcePath, string query, int limit)
    {
        MentionSearchRequestedEventArgs args = new(sourcePath, query, limit);
        MentionSearchRequested?.Invoke(this, args);
        return args.Targets.Select(ToMentionTarget).ToArray();
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

    private static MentionTarget ToMentionTarget(KnowledgeMentionTargetReadModel target)
    {
        return new MentionTarget(
            target.RelativePath,
            target.Title,
            Path.GetFileName(target.RelativePath),
            0);
    }
}
