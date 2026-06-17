namespace CogntApp.Views;

internal enum PdfStudyAnnotationKind
{
    Highlight = 1,
    Comment = 2,
}

internal sealed record PdfStudyAnnotation(
    string Id,
    int PageNumber,
    string SelectedText,
    double Left,
    double Top,
    double Width,
    double Height,
    PdfStudyAnnotationKind Kind,
    string? Comment,
    DateTimeOffset CreatedAtUtc);

internal sealed record PdfAnnotationExport(
    string SourcePath,
    string SourceTitle,
    IReadOnlyList<PdfStudyAnnotation> Annotations);

internal sealed record PdfAnnotationStoreDocument(
    string Schema,
    int Version,
    IReadOnlyList<PdfStudyAnnotation> Annotations)
{
    internal const string CurrentSchema = "cognt-pdf-annotations";
    internal const int CurrentVersion = 1;

    internal static PdfAnnotationStoreDocument Empty { get; } = new(
        CurrentSchema,
        CurrentVersion,
        []);
}

internal sealed record PdfTextWord(
    string Text,
    double Left,
    double Top,
    double Right,
    double Bottom);

internal sealed record PdfPageStudyData(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<PdfTextWord> Words);

internal sealed record PdfDocumentStudyData(
    IReadOnlyList<PdfPageStudyData> Pages);

internal sealed record RenderedPdfPage(
    int PageNumber,
    System.Windows.Media.Imaging.BitmapImage Image);

internal sealed record PdfTextSearchHit(
    int PageNumber,
    IReadOnlyList<int> WordIndexes);
