using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public partial class ImageMaskEditorView : Window
{
    private readonly Func<ImageMaskEditorResult, Task>? applyAsync;
    private readonly ILogger<ImageMaskEditorView>? logger;

    public ImageMaskEditorView(
        ImageMaskEditorViewModel viewModel,
        Func<ImageMaskEditorResult, Task>? applyAsync = null,
        ILogger<ImageMaskEditorView>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        this.applyAsync = applyAsync;
        this.logger = logger;
        InitializeComponent();
        DataContext = viewModel;
    }

    internal ImageMaskEditorResult? Result { get; private set; }

    private ImageMaskEditorViewModel ViewModel => (ImageMaskEditorViewModel)DataContext;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryValidateForSave(out string? message))
        {
            MessageBox.Show(
                this,
                message ?? "The image masks could not be saved.",
                "Image Occlusion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Result = ViewModel.BuildResult(saved: true);
        DialogResult = true;
        Close();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (applyAsync is null)
        {
            MessageBox.Show(
                this,
                "Use Save to persist image-mask changes.",
                "Image Occlusion",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!ViewModel.TryValidateForSave(out string? message))
        {
            MessageBox.Show(
                this,
                message ?? "The image masks could not be applied.",
                "Image Occlusion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            ImageMaskEditorResult result = ViewModel.BuildResult(saved: true);
            await applyAsync(result);
            ViewModel.MarkSaved();
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Image occlusion apply failed. SourceRelPath={SourceRelPath} ImageBlockId={ImageBlockId}",
                ViewModel.SourceRelPath,
                ViewModel.ImageBlockId);
            MessageBox.Show(
                this,
                ex.Message,
                "Image Occlusion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Result?.Saved != true && ViewModel.IsDirty)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "Discard unsaved image-mask changes?",
                "Image Occlusion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }
}
