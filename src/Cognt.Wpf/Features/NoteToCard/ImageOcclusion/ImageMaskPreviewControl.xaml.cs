using System.Windows;
using System.Windows.Controls;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public partial class ImageMaskPreviewControl : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(ImageMaskEditorViewModel),
        typeof(ImageMaskPreviewControl),
        new PropertyMetadata(null));

    public ImageMaskPreviewControl()
    {
        InitializeComponent();
    }

    public ImageMaskEditorViewModel? Editor
    {
        get => (ImageMaskEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }
}
