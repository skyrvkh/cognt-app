using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public void PostExtractResult(bool ok, string? createdPath = null, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowExtractResultAsync(ok, createdPath, error),
            "show extract result");
    }

    public void PostMentionTargets(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        mentionTargets = targets.ToList();
        RunBlockSuiteCommand(FlushMentionTargetsAsync(), "flush mention targets");
    }

    public void PostMentionPreviewResult(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent = null,
        string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionPreviewResultAsync(
                requestId,
                relativePath,
                mode,
                ok,
                documentContent,
                error),
            "post mention preview result");
    }

    public void PostDocumentLinks(KnowledgeDocumentLinksReadModel links)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.SetDocumentLinksAsync(links),
            "set document links");
    }

    public void PostMentionOpenResult(bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionOpenResultAsync(ok, error),
            "post mention open result");
    }

    public void PostSaveResult(bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowSaveResultAsync(ok, error),
            "show save result");
    }
}
