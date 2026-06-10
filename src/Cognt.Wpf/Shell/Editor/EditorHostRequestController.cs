using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class EditorHostRequestController
{
    internal EditorHostRequestController(
        MainViewModel viewModel,
        EditorView editorView,
        EditorDerivedDataRefreshController refreshes,
        Func<string, bool, Task<bool>> openNoteAsync,
        Func<string, string, string, Task> openImageOcclusionForImageAsync,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(refreshes);
        ArgumentNullException.ThrowIfNull(openNoteAsync);
        ArgumentNullException.ThrowIfNull(openImageOcclusionForImageAsync);

        _ = new EditorDocumentRequestController(
            viewModel,
            editorView,
            refreshes,
            openImageOcclusionForImageAsync,
            logger);
        _ = new EditorLinkRequestController(
            viewModel,
            editorView,
            openNoteAsync,
            logger);
        _ = new EditorAssetRequestController(
            viewModel,
            editorView,
            logger);
    }
}
