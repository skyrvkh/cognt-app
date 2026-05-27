using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskOverlayCanvas : Canvas
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(ImageMaskEditorViewModel),
        typeof(ImageMaskOverlayCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEditorChanged));

    public static readonly DependencyProperty SurfaceModeProperty = DependencyProperty.Register(
        nameof(SurfaceMode),
        typeof(ImageMaskSurfaceMode),
        typeof(ImageMaskOverlayCanvas),
        new FrameworkPropertyMetadata(ImageMaskSurfaceMode.Edit, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ImageMaskCoordinateMapper coordinateMapper = new();
    private ImageMaskViewRect imageContentRect;
    private Point? draftStartPoint;
    private Rect draftRect;
    private bool isDrawing;
    private string? draggingMaskId;
    private Point dragStartPoint;
    private Rect dragStartRect;
    private ResizeHandlePosition? activeHandle;
    private Rect resizeStartRect;
    private double resizeTotalDx;
    private double resizeTotalDy;
    private readonly List<Point> polygonSegmentBuffer = [];

    public ImageMaskOverlayCanvas()
    {
        Background = Brushes.Transparent;
        Focusable = true;
        SizeChanged += (_, _) => RefreshOverlayVisuals();
    }

    public ImageMaskEditorViewModel? Editor
    {
        get => (ImageMaskEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public ImageMaskSurfaceMode SurfaceMode
    {
        get => (ImageMaskSurfaceMode)GetValue(SurfaceModeProperty);
        set => SetValue(SurfaceModeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (Editor is null)
        {
            return;
        }

        imageContentRect = coordinateMapper.GetDisplayedImageRect(
            new ImageMaskViewSize(ActualWidth, ActualHeight),
            new ImageMaskViewSize(Editor.ImageSource.PixelWidth, Editor.ImageSource.PixelHeight));

        foreach (ImageMaskItemViewModel mask in Editor.Masks)
        {
            if (SurfaceMode == ImageMaskSurfaceMode.Preview)
            {
                if (!Editor.TryResolvePreviewMaskVisualState(mask.Mask, out ImageMaskVisualRole role, out bool isCovered))
                {
                    continue;
                }

                DrawMask(dc, mask, role, isCovered);
                continue;
            }

            DrawMask(dc, mask, mask.VisualRole, isCovered: true);
        }

        if (isDrawing)
        {
            DrawDraftMask(dc);
        }

        RefreshResizeHandles();
    }

    private static void OnEditorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ImageMaskOverlayCanvas canvas = (ImageMaskOverlayCanvas)dependencyObject;
        if (e.OldValue is ImageMaskEditorViewModel oldEditor)
        {
            oldEditor.PropertyChanged -= canvas.Editor_PropertyChanged;
            oldEditor.Masks.CollectionChanged -= canvas.Masks_CollectionChanged;
        }

        if (e.NewValue is ImageMaskEditorViewModel newEditor)
        {
            newEditor.PropertyChanged += canvas.Editor_PropertyChanged;
            newEditor.Masks.CollectionChanged += canvas.Masks_CollectionChanged;
        }

        canvas.RefreshOverlayVisuals();
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageMaskEditorViewModel.SelectedMaskId)
            or nameof(ImageMaskEditorViewModel.CurrentTool)
            or nameof(ImageMaskEditorViewModel.PreviewSlotId)
            or nameof(ImageMaskEditorViewModel.SelectedTargetOption))
        {
            RefreshOverlayVisuals();
        }
    }

    private void Masks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshOverlayVisuals();
    }

    private void RefreshOverlayVisuals()
    {
        InvalidateVisual();
    }
}
