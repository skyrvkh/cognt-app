using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace Cognt.Wpf.EditorHost.Services;

internal interface IBlockSuiteHostClient
{
    Task LoadNoteAsync(string? path, string title, string content, string? collectionRoot);

    Task ActivateEditorAsync();

    Task SetReadOnlyModeAsync(bool isReadOnly);

    Task FocusEditorAsync();

    Task FocusBlockAsync(string blockId);

    Task CreateClozeFromSelectionAsync(
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null);

    Task<SelectedImageBlockReadModel?> GetSelectedImageBlockAsync();

    Task<SelectedCardContextBlocksReadModel?> GetSelectedCardContextBlocksAsync();

    Task ShowSaveResultAsync(bool ok, string? error);

    Task ShowExtractResultAsync(bool ok, string? createdPath, string? error);

    Task SetMentionTargetsAsync(IEnumerable<KnowledgeMentionTargetReadModel> targets);

    Task SetDocumentLinksAsync(KnowledgeDocumentLinksReadModel links);

    Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent,
        string? error);

    Task HandleMentionOpenResultAsync(bool ok, string? error);
}
