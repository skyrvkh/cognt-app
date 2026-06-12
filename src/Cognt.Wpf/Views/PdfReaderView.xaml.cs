using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace CogntApp.Views;

public partial class PdfReaderView : UserControl
{
    private const int RenderDpi = 150;
    private const double DpiScale = 96d / RenderDpi;
    private readonly ILogger<PdfReaderView>? logger;
    private readonly PDFtoImage.RenderOptions renderOptions = new()
    {
        Dpi = RenderDpi,
        WithAnnotations = true,
        WithFormFill = true,
        WithAspectRatio = true,
    };

    private CancellationTokenSource? renderCancellation;
    private IReadOnlyList<RenderedPdfPage> renderedPages = [];
    private double zoom = 1d;

    public PdfReaderView(ILogger<PdfReaderView>? logger = null)
    {
        this.logger = logger;
        InitializeComponent();
        ShowEmptyState();
    }

    internal async Task LoadPdfAsync(KnowledgePdfDocumentReadModel? document, string? documentPath)
    {
        CancelRender();

        if (document is null || string.IsNullOrWhiteSpace(documentPath))
        {
            ShowEmptyState();
            return;
        }

        if (!File.Exists(documentPath))
        {
            logger?.LogWarning(
                "PDF reader load skipped because file is missing. RelativePath={RelativePath} DocumentPath={DocumentPath}",
                document.RelativePath,
                documentPath);
            ShowState(
                "Missing PDF file",
                "The selected PDF file is missing from workspace storage.");
            return;
        }

        PdfTitleText.Text = document.Title;
        ShowState("Loading PDF", "Rendering pages...");

        using CancellationTokenSource cancellation = new();
        renderCancellation = cancellation;

        try
        {
            byte[] pdfBytes = await File.ReadAllBytesAsync(documentPath, cancellation.Token).ConfigureAwait(true);
            IReadOnlyList<RenderedPdfPage> pages = await Task.Run(
                () => RenderPages(pdfBytes, cancellation.Token),
                cancellation.Token).ConfigureAwait(true);
            if (renderCancellation != cancellation)
            {
                return;
            }

            renderedPages = pages;
            RenderPageHost();
            StatePanel.Visibility = Visibility.Collapsed;
            PageScrollViewer.Visibility = Visibility.Visible;
            PdfStatusText.Text = $"{pages.Count} page{(pages.Count == 1 ? string.Empty : "s")} rendered with PDFium";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "PDF reader load failed. RelativePath={RelativePath} DocumentPath={DocumentPath}", document.RelativePath, documentPath);
            ShowState("Failed to load PDF", ex.Message);
        }
        finally
        {
            if (renderCancellation == cancellation)
            {
                renderCancellation = null;
            }
        }
    }

    internal Task LoadPdfFileAsync(string documentPath)
    {
        KnowledgePdfDocumentReadModel document = new(
            documentPath,
            Path.GetFileNameWithoutExtension(documentPath));
        return LoadPdfAsync(document, documentPath);
    }

    private IReadOnlyList<RenderedPdfPage> RenderPages(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        List<RenderedPdfPage> pages = [];
        int pageNumber = 1;

        foreach (SKBitmap bitmap in Conversion.ToImages(pdfBytes, password: null, options: renderOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (bitmap)
            using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, quality: 95))
            {
                pages.Add(new RenderedPdfPage(
                    pageNumber,
                    bitmap.Width,
                    CreateFrozenBitmap(data.ToArray())));
            }

            pageNumber++;
        }

        return pages;
    }

    private static BitmapImage CreateFrozenBitmap(byte[] pngBytes)
    {
        BitmapImage image = new();
        using MemoryStream stream = new(pngBytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void RenderPageHost()
    {
        PageHost.Children.Clear();
        foreach (RenderedPdfPage page in renderedPages)
        {
            PageHost.Children.Add(CreatePageElement(page));
        }

        ZoomText.Text = $"{zoom:P0}";
        ZoomOutButton.IsEnabled = zoom > 0.5d;
        ZoomInButton.IsEnabled = zoom < 2.5d;
    }

    private FrameworkElement CreatePageElement(RenderedPdfPage page)
    {
        StackPanel pagePanel = new();
        pagePanel.Children.Add(new TextBlock
        {
            Text = $"Page {page.Number}",
            Margin = new Thickness(0, 0, 0, 6),
            FontSize = 10,
            Foreground = TryFindResource("DimBrush") as Brush ?? Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        pagePanel.Children.Add(new Image
        {
            Source = page.Image,
            Width = page.PixelWidth * DpiScale * zoom,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
        });

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 18),
            Padding = new Thickness(8),
            Background = Brushes.White,
            BorderBrush = TryFindResource("LineBrush") as Brush ?? Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = pagePanel,
        };
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        zoom = Math.Max(0.5d, zoom - 0.1d);
        RenderPageHost();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        zoom = Math.Min(2.5d, zoom + 0.1d);
        RenderPageHost();
    }

    private void ShowEmptyState()
    {
        renderedPages = [];
        PdfTitleText.Text = "PDF Reader";
        ShowState("No PDF selected", "Select or drop a PDF to read it beside the current note.");
    }

    private void ShowState(string title, string message)
    {
        PageScrollViewer.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
        PdfStatusText.Text = message;
        PageHost.Children.Clear();
    }

    private void CancelRender()
    {
        renderCancellation?.Cancel();
        renderCancellation?.Dispose();
        renderCancellation = null;
    }

    private sealed record RenderedPdfPage(int Number, int PixelWidth, BitmapImage Image);
}
