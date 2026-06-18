using System.Text.Json;
using CefSharp;
using CogntApp.Shell.Browser;
using Microsoft.Extensions.Logging;

namespace CogntApp.Views;

public partial class BrowserPaneView
{
    private const string BrowserPageExtractionScript =
        """
        JSON.stringify((() => {
            const resolveUrl = value => {
                try { return value ? new URL(value, document.baseURI).href : ''; }
                catch { return String(value || ''); }
            };
            const clean = value => String(value || '')
                .replace(/\u00a0/g, ' ')
                .replace(/\s+/g, ' ')
                .trim();
            const escape = value => clean(value).replace(/\]/g, '\\]');
            const blocks = [];
            const push = value => { const text = clean(value); if (text) blocks.push(text); };
            const root = document.querySelector('main') || document.querySelector('article') || document.body;
            for (const element of Array.from(root ? root.querySelectorAll('h1,h2,h3,h4,h5,h6,p,li,img,pre,blockquote') : [])) {
                const tag = element.tagName.toLowerCase();
                if (tag === 'img') {
                    const src = resolveUrl(element.currentSrc || element.getAttribute('src') || '');
                    if (src) blocks.push(`![${escape(element.alt || element.title || 'Image')}](${src})`);
                    continue;
                }

                if (tag === 'pre') {
                    const text = (element.innerText || element.textContent || '').trim();
                    if (text) blocks.push('```\n' + text + '\n```');
                    continue;
                }

                if (tag === 'blockquote') {
                    const text = (element.innerText || element.textContent || '').trim();
                    if (text) blocks.push(text.split(/\r?\n/).map(line => `> ${line.trim()}`).join('\n'));
                    continue;
                }

                if (/^h[1-6]$/.test(tag)) {
                    const text = clean(element.innerText || element.textContent || '');
                    if (text) blocks.push(`${'#'.repeat(Number(tag.slice(1)))} ${text}`);
                    continue;
                }

                push((tag === 'li' ? '- ' : '') + (element.innerText || element.textContent || ''));
            }

            return {
                title: document.title || '',
                url: location.href || '',
                text: document.body ? String(document.body.innerText || document.body.textContent || '') : '',
                markdown: blocks.join('\n\n').trim(),
                html: document.documentElement ? document.documentElement.outerHTML : '',
                images: Array.from(document.images || []).map((image, index) => ({
                    index,
                    source: resolveUrl(image.getAttribute('src') || image.src || ''),
                    currentSource: resolveUrl(image.currentSrc || ''),
                    sourceSet: image.getAttribute('srcset') || '',
                    alt: image.getAttribute('alt') || '',
                    title: image.getAttribute('title') || '',
                    naturalWidth: image.naturalWidth || 0,
                    naturalHeight: image.naturalHeight || 0,
                    lazySource: resolveUrl(image.getAttribute('data-src') || image.getAttribute('data-original') || image.getAttribute('data-lazy-src') || ''),
                    lazySourceSet: image.getAttribute('data-srcset') || image.getAttribute('data-lazy-srcset') || ''
                }))
            };
        })())
        """;

    private static readonly JsonSerializerOptions BrowserExtractionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private async Task ExtractCurrentPageToNoteAsync()
    {
        if (browserPageNoteCreator is null)
        {
            SetBrowserStatus("Page extraction is unavailable.");
            return;
        }

        try
        {
            SetBrowserStatus("Extracting page...");
            BrowserPageExtraction extraction = await ExtractCurrentPageAsync().ConfigureAwait(true);
            string relativePath = await browserPageNoteCreator(extraction).ConfigureAwait(true);
            SetBrowserStatus($"Extracted page to new note: {relativePath}");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Browser page extraction failed. Url={Url}", GetCurrentPageUrl());
            SetBrowserStatus($"Extract failed: {ex.Message}");
        }
    }

    private async Task<BrowserPageExtraction> ExtractCurrentPageAsync()
    {
        string? currentUrl = GetCurrentPageUrl();
        BrowserTabSession? session = ActiveSession;
        if (session is null
            || session.IsLoading
            || !IsAllowedWebUri(currentUrl))
        {
            throw new InvalidOperationException("Open an http or https page before extracting browser content.");
        }

        await SaveBrowserStateAsync().ConfigureAwait(true);
        JavascriptResponse response = await session.Browser
            .EvaluateScriptAsync(BrowserPageExtractionScript)
            .ConfigureAwait(true);
        if (!response.Success || response.Result is not string scriptResult)
        {
            throw new InvalidOperationException(response.Message ?? "The page extraction script failed.");
        }

        BrowserPageScriptResult page = JsonSerializer.Deserialize<BrowserPageScriptResult>(
            scriptResult,
            BrowserExtractionJsonOptions) ?? throw new InvalidOperationException("The page did not return extractable content.");

        string pageUrl = IsAllowedWebUri(page.Url) ? page.Url! : currentUrl!;
        IReadOnlyList<BrowserPageImageExtraction> images = NormalizeExtractedImages(page.Images);
        if (string.IsNullOrWhiteSpace(page.Text)
            && string.IsNullOrWhiteSpace(page.Markdown)
            && string.IsNullOrWhiteSpace(page.Html)
            && images.Count == 0)
        {
            throw new InvalidOperationException("The browser page did not expose extractable content.");
        }

        return new BrowserPageExtraction(
            NormalizeExtractedSingleLine(page.Title) is { Length: > 0 } title ? title : CreateVisibleTabTitle(),
            pageUrl,
            NormalizeExtractedMultiline(page.Text),
            NormalizeExtractedMultiline(page.Markdown),
            NormalizeExtractedHtml(page.Html),
            [],
            images,
            DateTimeOffset.UtcNow);
    }

    private bool CanExtractCurrentPage()
    {
        return browserPageNoteCreator is not null
            && ActiveSession is { IsLoading: false }
            && IsAllowedWebUri(GetCurrentPageUrl());
    }

    private static IReadOnlyList<BrowserPageImageExtraction> NormalizeExtractedImages(
        IReadOnlyList<BrowserPageScriptImageResult>? images)
    {
        if (images is null || images.Count == 0)
        {
            return [];
        }

        List<BrowserPageImageExtraction> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (BrowserPageScriptImageResult image in images)
        {
            string source = NormalizeExtractedSingleLine(image.Source);
            string currentSource = NormalizeExtractedSingleLine(image.CurrentSource);
            string lazySource = NormalizeExtractedSingleLine(image.LazySource);
            string identity = string.Join("|", source, currentSource, lazySource, image.SourceSet, image.LazySourceSet);
            if (string.IsNullOrWhiteSpace(identity.Replace("|", string.Empty, StringComparison.Ordinal))
                || !seen.Add(identity))
            {
                continue;
            }

            normalized.Add(new BrowserPageImageExtraction(
                image.Index,
                source,
                currentSource,
                NormalizeExtractedSingleLine(image.SourceSet),
                NormalizeExtractedSingleLine(image.Alt),
                NormalizeExtractedSingleLine(image.Title),
                Math.Max(0, image.NaturalWidth),
                Math.Max(0, image.NaturalHeight),
                lazySource,
                NormalizeExtractedSingleLine(image.LazySourceSet)));
        }

        return normalized;
    }

    private static string NormalizeExtractedHtml(string? html)
    {
        return string.IsNullOrWhiteSpace(html) ? string.Empty : html.Replace('\0', ' ').Trim();
    }

    private static string NormalizeExtractedMultiline(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace('\0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        while (normalized.Contains("\n\n\n\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\n\n\n\n", "\n\n\n", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string NormalizeExtractedSingleLine(string? value)
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

    private sealed record BrowserPageScriptResult(
        string? Title,
        string? Url,
        string? Text,
        string? Markdown,
        string? Html,
        IReadOnlyList<BrowserPageScriptImageResult>? Images);

    private sealed record BrowserPageScriptImageResult(
        int Index,
        string? Source,
        string? CurrentSource,
        string? SourceSet,
        string? Alt,
        string? Title,
        int NaturalWidth,
        int NaturalHeight,
        string? LazySource,
        string? LazySourceSet);
}
