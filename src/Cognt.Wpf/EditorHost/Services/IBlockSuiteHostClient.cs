using CogntApp.Models.Markdown;

namespace CogntApp.Services;

public interface IBlockSuiteHostClient
{
    Task LoadNoteAsync(string? path, string title, string content, string? collectionRoot = null);

    Task LoadCardAsync(string content);

    Task BeginCardRenderAsync();

    Task ActivateEditorAsync();

    Task FocusEditorAsync();

    Task ShowSaveResultAsync(bool ok, string? error);

    Task ShowExtractResultAsync(bool ok, string? createdPath, string? error);

    Task SetMentionTargetsAsync(IEnumerable<MentionTarget> targets);

    Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? title,
        string? documentContent,
        string? error);

    Task HandleMentionOpenResultAsync(bool ok, string? error);
}
