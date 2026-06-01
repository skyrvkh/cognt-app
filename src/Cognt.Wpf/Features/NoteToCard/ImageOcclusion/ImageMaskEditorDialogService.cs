using System.Windows;
using Microsoft.Extensions.Logging;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed class ImageMaskEditorDialogService : IImageMaskEditorDialogService
{
    private readonly ILoggerFactory loggerFactory;

    public ImageMaskEditorDialogService(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public Task<ImageMaskEditorResult> ShowAsync(
        OpenImageMaskEditorRequest request,
        Func<ImageMaskEditorResult, Task>? applyAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();


        ImageMaskEditorViewModel viewModel = new(request);
        ImageMaskEditorView view = new(
            viewModel,
            applyAsync,
            loggerFactory.CreateLogger<ImageMaskEditorView>())
        {
            Owner = Application.Current?.MainWindow,
        };

        bool? dialogResult = view.ShowDialog();
        ImageMaskEditorResult result = view.Result
            ?? new ImageMaskEditorResult(
                Saved: dialogResult == true,
                request.SourceRelPath,
                request.ImageBlockId,
                request.ContextId ?? request.Context.Id,
                request.Context,
                request.Context.Images.FirstOrDefault(image =>
                    string.Equals(image.ImageBlockId, request.ImageBlockId, StringComparison.Ordinal))?.Masks ?? []);


        return Task.FromResult(result);
    }
}
