using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Shell.Browser;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private const int MaxBrowserPageImages = 24;
    private const long MaxBrowserPageImageBytes = 8 * 1024 * 1024;
    private const long MaxBrowserPageImageTotalBytes = 40 * 1024 * 1024;
    private static readonly HttpClient BrowserImageHttpClient = CreateBrowserImageHttpClient();

    internal async Task<string> CreateBrowserPageNoteAsync(BrowserPageExtraction extraction)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before extracting browser content.");
        }

        BrowserPageExtraction normalized = NormalizeBrowserExtraction(extraction);
        SaveCurrentNoteFromEditor();

        string collectionPath = openCollectionPath;
        string initialContent = BrowserPageDocumentBuilder.Build(
            normalized,
            new Dictionary<int, BrowserPageAttachedImage>());
        KnowledgeCreatedNoteReadModel createdNote = await Task.Run(() => noteService.CreateNote(
            collectionPath,
            parentId: null,
            normalized.Title,
            initialContent));

        IReadOnlyDictionary<int, BrowserPageAttachedImage> attachedImages = await AttachBrowserPageImagesAsync(
            collectionPath,
            createdNote.RelativePath,
            normalized);
        if (attachedImages.Count > 0)
        {
            string finalContent = BrowserPageDocumentBuilder.Build(normalized, attachedImages);
            await SaveExtractedBrowserNoteAsync(collectionPath, createdNote.RelativePath, finalContent);
        }

        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
        return createdNote.RelativePath;
    }

    private async Task SaveExtractedBrowserNoteAsync(
        string collectionPath,
        string relativePath,
        string content)
    {
        await noteSaveSemaphore.WaitAsync();
        try
        {
            await Task.Run(() => noteService.SaveNote(collectionPath, relativePath, content));
        }
        finally
        {
            noteSaveSemaphore.Release();
        }
    }

    private static BrowserPageExtraction NormalizeBrowserExtraction(BrowserPageExtraction extraction)
    {
        string url = NormalizeBrowserSingleLine(extraction.Url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The browser page URL is empty.");
        }

        string title = NormalizeBrowserSingleLine(extraction.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = string.IsNullOrWhiteSpace(uri.Host) ? "Browser page" : uri.Host;
        }

        return extraction with
        {
            Title = TruncateBrowserTitle(title),
            Url = uri.AbsoluteUri,
            Text = NormalizeBrowserMultiline(extraction.Text),
            Markdown = NormalizeBrowserMultiline(extraction.Markdown),
            Html = string.Empty,
            Blocks = NormalizeBrowserBlocks(extraction.Blocks),
            Images = NormalizeBrowserImages(extraction.Images),
        };
    }

    private static IReadOnlyList<BrowserPageContentBlock> NormalizeBrowserBlocks(
        IReadOnlyList<BrowserPageContentBlock>? blocks)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return [];
        }

        HashSet<string> allowedKinds =
        [
            BrowserPageContentBlockKinds.Paragraph,
            BrowserPageContentBlockKinds.Heading,
            BrowserPageContentBlockKinds.BulletedListItem,
            BrowserPageContentBlockKinds.NumberedListItem,
            BrowserPageContentBlockKinds.Quote,
            BrowserPageContentBlockKinds.Code,
            BrowserPageContentBlockKinds.Table,
            BrowserPageContentBlockKinds.Image,
        ];
        List<BrowserPageContentBlock> normalized = [];
        foreach (BrowserPageContentBlock block in blocks)
        {
            string kind = NormalizeBrowserSingleLine(block.Kind).ToLowerInvariant();
            if (!allowedKinds.Contains(kind))
            {
                continue;
            }

            string text = kind is BrowserPageContentBlockKinds.Code or BrowserPageContentBlockKinds.Table
                ? NormalizeBrowserMultiline(block.Text)
                : NormalizeBrowserSingleLine(block.Text);
            int imageIndex = kind == BrowserPageContentBlockKinds.Image ? block.ImageIndex : -1;
            if (kind != BrowserPageContentBlockKinds.Image && string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            normalized.Add(new BrowserPageContentBlock(
                kind,
                text,
                kind == BrowserPageContentBlockKinds.Heading ? Math.Clamp(block.Level, 1, 6) : 0,
                kind == BrowserPageContentBlockKinds.Code ? NormalizeBrowserSingleLine(block.Language) : string.Empty,
                imageIndex));
        }

        return normalized;
    }

    private async Task<IReadOnlyDictionary<int, BrowserPageAttachedImage>> AttachBrowserPageImagesAsync(
        string collectionPath,
        string noteRelativePath,
        BrowserPageExtraction extraction)
    {
        Dictionary<int, BrowserPageAttachedImage> attachedImages = [];
        HashSet<int> referencedIndexes = extraction.Blocks
            .Where(block => block.Kind == BrowserPageContentBlockKinds.Image && block.ImageIndex >= 0)
            .Select(block => block.ImageIndex)
            .ToHashSet();
        long remainingBytes = MaxBrowserPageImageTotalBytes;

        foreach (BrowserPageImageExtraction image in extraction.Images)
        {
            if (!referencedIndexes.Contains(image.Index)
                || attachedImages.Count >= MaxBrowserPageImages
                || remainingBytes <= 0)
            {
                continue;
            }

            try
            {
                BrowserImageDownload? download = null;
                foreach (string source in EnumerateBrowserImageSources(image))
                {
                    download = await DownloadBrowserImageAsync(
                        source,
                        extraction.Url,
                        image.Index + 1,
                        Math.Min(MaxBrowserPageImageBytes, remainingBytes));
                    if (download is not null)
                    {
                        break;
                    }
                }

                if (download is null)
                {
                    continue;
                }

                await using MemoryStream imageContent = new(download.Content, writable: false);
                KnowledgeAttachmentReadModel attachment = await noteService.AttachFileReferenceToNoteAsync(
                    collectionPath,
                    noteRelativePath,
                    download.FileName,
                    imageContent);
                if (attachment.Kind != KnowledgeAttachmentKind.Image)
                {
                    continue;
                }

                attachedImages[image.Index] = new BrowserPageAttachedImage(
                    image.Index,
                    string.IsNullOrWhiteSpace(image.Alt) ? string.Empty : image.Alt,
                    attachment.RelativePath,
                    download.Content.LongLength,
                    image.NaturalWidth,
                    image.NaturalHeight);
                remainingBytes -= download.Content.LongLength;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Browser page image import failed. PageUrl={PageUrl} ImageIndex={ImageIndex}",
                    extraction.Url,
                    image.Index);
            }
        }

        return attachedImages;
    }

    private static IEnumerable<string> EnumerateBrowserImageSources(BrowserPageImageExtraction image)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string source in new[] { image.CurrentSource, image.LazySource, image.Source })
        {
            if (!string.IsNullOrWhiteSpace(source) && seen.Add(source))
            {
                yield return source;
            }
        }

        foreach (string sourceSet in new[] { image.LazySourceSet, image.SourceSet })
        {
            foreach (string candidate in ParseSourceSet(sourceSet).Reverse())
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> ParseSourceSet(string sourceSet)
    {
        if (string.IsNullOrWhiteSpace(sourceSet))
        {
            yield break;
        }

        foreach (string entry in sourceSet.Split(','))
        {
            string candidate = entry.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static async Task<BrowserImageDownload?> DownloadBrowserImageAsync(
        string source,
        string pageUrl,
        int imageNumber,
        long maximumBytes)
    {
        if (TryReadBrowserDataImage(source, maximumBytes, out byte[]? dataContent, out string? dataExtension))
        {
            return new BrowserImageDownload(dataContent!, $"browser-image-{imageNumber}{dataExtension}");
        }

        Uri? sourceUri = Uri.TryCreate(source, UriKind.Absolute, out Uri? absoluteSource)
            ? absoluteSource
            : Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? pageUri)
                ? new Uri(pageUri, source)
                : null;
        if (sourceUri is null || sourceUri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*", 0.9));
        if (Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? referrer))
        {
            request.Headers.Referrer = referrer;
        }

        using HttpResponseMessage response = await BrowserImageHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentLength is long contentLength && contentLength > maximumBytes)
        {
            return null;
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync();
        await using MemoryStream content = new();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await responseStream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            if (content.Length + read > maximumBytes)
            {
                return null;
            }

            await content.WriteAsync(buffer.AsMemory(0, read));
        }

        byte[] bytes = content.ToArray();
        if (!TryDetectBrowserImageExtension(
            bytes,
            sourceUri,
            response.Content.Headers.ContentType?.MediaType,
            out string? extension))
        {
            return null;
        }

        return new BrowserImageDownload(bytes, $"browser-image-{imageNumber}{extension}");
    }

    private static bool TryReadBrowserDataImage(
        string source,
        long maximumBytes,
        out byte[]? content,
        out string? extension)
    {
        content = null;
        extension = null;
        if (!source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int commaIndex = source.IndexOf(',');
        if (commaIndex <= 0)
        {
            return false;
        }

        string header = source[..commaIndex];
        string payload = source[(commaIndex + 1)..];
        try
        {
            byte[] bytes = header.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(payload)
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            if (bytes.LongLength == 0
                || bytes.LongLength > maximumBytes
                || !TryDetectBrowserImageExtension(bytes, sourceUri: null, header, out string? detectedExtension))
            {
                return false;
            }

            content = bytes;
            extension = detectedExtension;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDetectBrowserImageExtension(
        ReadOnlySpan<byte> content,
        Uri? sourceUri,
        string? contentType,
        out string? extension)
    {
        if (content.Length >= 8
            && content[0] == 0x89
            && content[1] == 0x50
            && content[2] == 0x4E
            && content[3] == 0x47)
        {
            extension = ".png";
            return true;
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }

        if (content.Length >= 6
            && (content[..6].SequenceEqual("GIF87a"u8) || content[..6].SequenceEqual("GIF89a"u8)))
        {
            extension = ".gif";
            return true;
        }

        if (content.Length >= 12
            && content[..4].SequenceEqual("RIFF"u8)
            && content.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            extension = ".webp";
            return true;
        }

        string sourceExtension = sourceUri is null
            ? string.Empty
            : Path.GetExtension(sourceUri.AbsolutePath).ToLowerInvariant();
        extension = sourceExtension switch
        {
            ".png" => ".png",
            ".jpg" or ".jpeg" => ".jpg",
            ".webp" => ".webp",
            ".gif" => ".gif",
            _ => contentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => null,
            },
        };
        return extension is not null && content.Length > 0;
    }

    private static HttpClient CreateBrowserImageHttpClient()
    {
        HttpClient client = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/137.0.0.0 Safari/537.36 Cognt/1.0");
        return client;
    }

    private static IReadOnlyList<BrowserPageImageExtraction> NormalizeBrowserImages(
        IReadOnlyList<BrowserPageImageExtraction>? images)
    {
        if (images is null || images.Count == 0)
        {
            return [];
        }

        List<BrowserPageImageExtraction> normalized = [];
        HashSet<int> seenIndexes = [];
        foreach (BrowserPageImageExtraction image in images)
        {
            if (image.Index < 0 || !seenIndexes.Add(image.Index))
            {
                continue;
            }

            normalized.Add(image with
            {
                Source = NormalizeBrowserSingleLine(image.Source),
                CurrentSource = NormalizeBrowserSingleLine(image.CurrentSource),
                SourceSet = NormalizeBrowserSingleLine(image.SourceSet),
                Alt = NormalizeBrowserSingleLine(image.Alt),
                Title = NormalizeBrowserSingleLine(image.Title),
                NaturalWidth = Math.Max(0, image.NaturalWidth),
                NaturalHeight = Math.Max(0, image.NaturalHeight),
                LazySource = NormalizeBrowserSingleLine(image.LazySource),
                LazySourceSet = NormalizeBrowserSingleLine(image.LazySourceSet),
            });
        }

        return normalized;
    }

    private static string NormalizeBrowserMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Replace('\0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        while (normalized.Contains("\n\n\n\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\n\n\n\n", "\n\n\n", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string NormalizeBrowserSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Replace('\0', ' ').Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string TruncateBrowserTitle(string title)
    {
        const int maximumTitleLength = 120;
        return title.Length <= maximumTitleLength
            ? title
            : string.Concat(title.AsSpan(0, maximumTitleLength - 3), "...");
    }

    private sealed record BrowserImageDownload(byte[] Content, string FileName);
}
