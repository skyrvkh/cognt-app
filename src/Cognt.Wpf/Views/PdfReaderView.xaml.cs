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

        this.documentPath = documentPath;
        sourceTitle = document.Title;
        annotationStorePath = Path.ChangeExtension(documentPath, ".cognt-annotations.json");
        PdfTitleText.Text = sourceTitle;
        ShowState("Loading PDF", "Reading text and page layout...");

        using CancellationTokenSource cancellation = new();
        documentCancellation = cancellation;

        try
        {
            PdfDocumentStudyData studyData = await Task.Run(
                () => ReadStudyData(documentPath, cancellation.Token),
                cancellation.Token).ConfigureAwait(true);
            if (!IsCurrentDocumentLoad(cancellation))
            {
                return;
            }

            pages = studyData.Pages;
            LoadAnnotations();
            if (pages.Count == 0)
            {
                return;
            }

            currentPageNumber = 1;
            StatePanel.Visibility = Visibility.Collapsed;
            PageScrollViewer.Visibility = Visibility.Visible;
            await ShowPageAsync(currentPageNumber).ConfigureAwait(true);
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
            if (ReferenceEquals(documentCancellation, cancellation))
            {
                documentCancellation = null;
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

    private static PdfDocumentStudyData ReadStudyData(string filePath, CancellationToken cancellationToken)
    {
        using PdfDocument document = PdfDocument.Open(filePath);
        List<PdfPageStudyData> result = new(document.NumberOfPages);
        for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UglyToad.PdfPig.Content.Page page = document.GetPage(pageNumber);
            PdfTextWord[] words = page.GetWords()
                .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                .Select(word => new PdfTextWord(
                    word.Text,
                    word.BoundingBox.Left,
                    word.BoundingBox.Top,
                    word.BoundingBox.Right,
                    word.BoundingBox.Bottom))
                .OrderByDescending(word => word.Top)
                .ThenBy(word => word.Left)
                .ToArray();
            result.Add(new PdfPageStudyData(pageNumber, page.Width, page.Height, words));
        }

        return new PdfDocumentStudyData(result);
    }
                ShowState("No PDF pages", "The selected file did not contain readable pages.");

    private async Task ShowPageAsync(int requestedPageNumber)
    {
        if (pages.Count == 0 || documentPath is null)
        {
            return;
        }

        int pageNumber = Math.Clamp(requestedPageNumber, 1, pages.Count);
        currentPageNumber = pageNumber;
        selectedWordIndexes.Clear();
        selectionStart = null;

        if (!renderedPages.TryGetValue(pageNumber, out RenderedPdfPage? renderedPage))
        {
            CancellationToken cancellationToken = documentCancellation?.Token ?? CancellationToken.None;
            int requestId = ++pageRenderRequestId;
            PdfStatusText.Text = $"Rendering page {pageNumber} of {pages.Count}...";
            renderedPage = await Task.Run(
                () => RenderPage(documentPath, pageNumber, cancellationToken),
                cancellationToken).ConfigureAwait(true);
            if (requestId != pageRenderRequestId || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            renderedPages[pageNumber] = renderedPage;
            TrimRenderedPageCache(pageNumber);
        }

        RenderCurrentPage(renderedPage);
    }

    private static RenderedPdfPage RenderPage(string filePath, int pageNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using PdfDocument document = PdfDocument.Open(filePath);
        document.AddSkiaPageFactory();
        using SKBitmap bitmap = document.GetPageAsSKBitmap(pageNumber, RenderScale, SKColors.White);
        cancellationToken.ThrowIfCancellationRequested();
        return new RenderedPdfPage(pageNumber, CreateFrozenBitmap(bitmap));
    }

    private static BitmapImage CreateFrozenBitmap(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, quality: 95);
        BitmapImage result = new();
        using MemoryStream stream = new(data.ToArray());
        result.BeginInit();
        result.CacheOption = BitmapCacheOption.OnLoad;
        result.StreamSource = stream;
        result.EndInit();
        result.Freeze();
        return result;
    }

    private void RenderCurrentPage(RenderedPdfPage renderedPage)
    {
        PdfPageStudyData? page = GetCurrentPageData();
        if (page is null)
        {
            return;
        }

        double pageWidth = Math.Max(1d, page.Width * PdfPointToDip * zoom);
        double pageHeight = Math.Max(1d, page.Height * PdfPointToDip * zoom);
        Canvas canvas = new()
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
            Cursor = Cursors.IBeam,
            Tag = page,
        };
        canvas.PreviewMouseLeftButtonDown += PageCanvas_PreviewMouseLeftButtonDown;
        canvas.PreviewMouseMove += PageCanvas_PreviewMouseMove;
        canvas.PreviewMouseLeftButtonUp += PageCanvas_PreviewMouseLeftButtonUp;

        Image image = new()
        {
            Source = renderedPage.Image,
            Width = pageWidth,
            Height = pageHeight,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
        };
        canvas.Children.Add(image);
        RenderAnnotations(canvas, page);
        RenderSearchHighlight(canvas, page);
        RenderTextSelection(canvas, page);

        Border pageBorder = new()
        {
            Padding = new Thickness(8),
            Background = Brushes.White,
            BorderBrush = TryFindResource("LineBrush") as Brush ?? Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = canvas,
        };
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        zoom = Math.Max(0.5d, zoom - 0.1d);
        RefreshCurrentPage();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        zoom = Math.Min(2.5d, zoom + 0.1d);
        RefreshCurrentPage();
    }

    private void RefreshCurrentPage()
    {
        if (renderedPages.TryGetValue(currentPageNumber, out RenderedPdfPage? renderedPage))
        {
            RenderCurrentPage(renderedPage);
        }
        else
        {
            _ = ShowPageAsync(currentPageNumber);
        }
    }

    private void RefreshControls()
    {
        bool hasPage = pages.Count > 0 && currentPageNumber > 0;
        bool hasSelection = selectedWordIndexes.Count > 0;
        PreviousPageButton.IsEnabled = hasPage && currentPageNumber > 1;
        NextPageButton.IsEnabled = hasPage && currentPageNumber < pages.Count;
        ZoomOutButton.IsEnabled = hasPage && zoom > 0.5d;
        ZoomInButton.IsEnabled = hasPage && zoom < 2.5d;
        CopySelectionButton.IsEnabled = hasSelection;
        HighlightSelectionButton.IsEnabled = hasSelection;
        AddCommentButton.IsEnabled = hasSelection;
        RemoveLatestAnnotationButton.IsEnabled = hasPage && annotations.Any(annotation => annotation.PageNumber == currentPageNumber);
        ExportAnnotationsButton.IsEnabled = exportAnnotations is not null && annotations.Count > 0;
        ZoomText.Text = $"{zoom:P0}";
    }

    private bool IsCurrentDocumentLoad(CancellationTokenSource cancellation)
    {
        return ReferenceEquals(documentCancellation, cancellation) && !cancellation.IsCancellationRequested;
    }

    private void ResetDocumentState()
    {
        renderedPages.Clear();
        annotations.Clear();
        selectedWordIndexes.Clear();
        searchHits.Clear();
        pages = [];
        documentPath = null;
        annotationStorePath = null;
        sourceTitle = "PDF Reader";
        currentPageNumber = 0;
        currentSearchHitIndex = -1;
        pageRenderRequestId++;
        PageHost.Children.Clear();
        SearchTextBox.Clear();
        SearchStatusText.Text = string.Empty;
        CommentTextBox.Clear();
    }

    private void ShowEmptyState()
    {
        PageScrollViewer.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Collapsed;
        PageHost.Children.Clear();
    }

    private void ShowState(string title, string message)
    {
        PageScrollViewer.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
        PdfStatusText.Text = message;
        PdfTitleText.Text = "PDF Reader";
        PdfStatusText.Text = string.Empty;
        PageText.Text = "Page 0 / 0";
        AnnotationCountText.Text = "0 annotations";
        RefreshControls();
    }

    private void CancelDocumentLoad()
    {
        documentCancellation?.Cancel();
        documentCancellation?.Dispose();
        documentCancellation = null;
    }
}
        RefreshControls();
        PageHost.Children.Clear();
        PageHost.Children.Add(pageBorder);

        int pageAnnotationCount = annotations.Count(annotation => annotation.PageNumber == page.PageNumber);
        PageText.Text = $"Page {page.PageNumber} / {pages.Count}";
        AnnotationCountText.Text = $"{annotations.Count} annotation{(annotations.Count == 1 ? string.Empty : "s")}";
        PdfStatusText.Text = page.Words.Count == 0
            ? $"Page {page.PageNumber} has no selectable text."
            : $"Page {page.PageNumber} · {page.Words.Count:N0} selectable words · {pageAnnotationCount} annotation{(pageAnnotationCount == 1 ? string.Empty : "s")}";
        RefreshControls();
