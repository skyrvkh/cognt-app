using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed record OpenImageMaskEditorRequest(
    string CollectionId,
    string SourceRelPath,
    string ImageBlockId,
    string ImageRelPath,
    string ImageFullPath,
    CardContext Context,
    string? ContextId = null);

public sealed record ImageMaskEditorResult(
    bool Saved,
    string SourceRelPath,
    string ImageBlockId,
    string? ContextId,
    CardContext UpdatedContext,
    IReadOnlyList<ImageOcclusionMask> Masks);

public interface IImageMaskEditorDialogService
{
    Task<ImageMaskEditorResult> ShowAsync(
        OpenImageMaskEditorRequest request,
        Func<ImageMaskEditorResult, Task>? applyAsync = null,
        CancellationToken cancellationToken = default);
}
