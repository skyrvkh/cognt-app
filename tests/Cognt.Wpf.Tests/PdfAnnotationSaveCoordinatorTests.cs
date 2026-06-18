using CogntApp.Shell.Browser;
using Xunit;

namespace Cognt.Wpf.Tests;

public sealed class PdfAnnotationSaveCoordinatorTests
{
    [Fact]
    public void TryCreateRequest_RejectsNonLocalPdfAddresses()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = Path.Combine(directory.Path, "source.pdf");
        File.WriteAllText(sourcePath, "original");

        Assert.False(PdfAnnotationSaveCoordinator.TryCreateRequest(
            "https://example.test/source.pdf",
            1,
            directory.Path,
            directory.DownloadsPath,
            out PdfAnnotationSaveRequest? request));
        Assert.Null(request);
    }

    [Fact]
    public void TryCreateRequest_RejectsLocalNonPdfAddresses()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = Path.Combine(directory.Path, "source.txt");
        File.WriteAllText(sourcePath, "original");

        Assert.False(PdfAnnotationSaveCoordinator.TryCreateRequest(
            new Uri(sourcePath).AbsoluteUri,
            1,
            directory.Path,
            directory.DownloadsPath,
            out PdfAnnotationSaveRequest? request));
        Assert.Null(request);
    }

    [Fact]
    public void Cancel_LeavesOriginalPdfUntouchedAndRemovesTemporaryOutput()
    {
        using TemporaryDirectory directory = new();
        PdfAnnotationSaveRequest request = CreateRequest(directory, "original", "annotated");

        PdfAnnotationSaveCoordinator.Cancel(request);

        Assert.Equal("original", File.ReadAllText(request.SourcePath));
        Assert.False(File.Exists(request.TemporaryPath));
    }

    [Fact]
    public void Complete_ReplacesOriginalPdfAndRemovesTemporaryOutput()
    {
        using TemporaryDirectory directory = new();
        PdfAnnotationSaveRequest request = CreateRequest(directory, "original", "annotated");

        PdfAnnotationSaveResult result = PdfAnnotationSaveCoordinator.Complete(request);

        Assert.True(result.ReplacedOriginal);
        Assert.Null(result.FallbackPath);
        Assert.Equal("annotated", File.ReadAllText(request.SourcePath));
        Assert.False(File.Exists(request.TemporaryPath));
    }

    [Fact]
    public void Complete_WhenOriginalIsMissing_PreservesAnnotatedPdfInDownloads()
    {
        using TemporaryDirectory directory = new();
        PdfAnnotationSaveRequest request = CreateRequest(directory, "original", "annotated");
        File.Delete(request.SourcePath);

        PdfAnnotationSaveResult result = PdfAnnotationSaveCoordinator.Complete(request);

        Assert.False(result.ReplacedOriginal);
        Assert.Equal(request.FallbackPath, result.FallbackPath);
        Assert.True(File.Exists(request.FallbackPath));
        Assert.Equal("annotated", File.ReadAllText(request.FallbackPath));
    }

    [Fact]
    public void TryCreateRequest_RejectsLocalPdfOutsideCollection()
    {
        using TemporaryDirectory directory = new();
        string externalPath = Path.Combine(Path.GetTempPath(), $"cognt-external-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(externalPath, "original");

        try
        {
            Assert.False(PdfAnnotationSaveCoordinator.TryCreateRequest(
                new Uri(externalPath).AbsoluteUri,
                6,
                directory.Path,
                directory.DownloadsPath,
                out PdfAnnotationSaveRequest? request));
            Assert.Null(request);
        }
        finally
        {
            File.Delete(externalPath);
        }
    }

    private static PdfAnnotationSaveRequest CreateRequest(TemporaryDirectory directory, string original, string annotated)
    {
        string sourcePath = Path.Combine(directory.Path, "source.pdf");
        File.WriteAllText(sourcePath, original);
        Assert.True(PdfAnnotationSaveCoordinator.TryCreateRequest(
            new Uri(sourcePath).AbsoluteUri,
            42,
            directory.Path,
            directory.DownloadsPath,
            out PdfAnnotationSaveRequest? request));
        File.WriteAllText(request!.TemporaryPath, annotated);
        return request;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cognt-pdf-tests-{Guid.NewGuid():N}");
            DownloadsPath = System.IO.Path.Combine(Path, "downloads");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string DownloadsPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
