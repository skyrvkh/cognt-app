using System.Text.Json;
using CefSharp;
using CogntApp.Shell.Browser;
using Microsoft.Extensions.Logging;

namespace CogntApp.Views;

public partial class BrowserPaneView
{
    private const string BrowserPageExtractionScript =
        """
        return JSON.stringify(await (async () => {
            const resolveUrl = value => {
                try { return value ? new URL(value, document.baseURI).href : ''; }
                catch { return String(value || ''); }
            };
            const cleanInline = value => String(value || '')
                .replace(/\u00a0/g, ' ')
                .replace(/\s+/g, ' ')
                .trim();
            const cleanCode = value => String(value || '')
                .replace(/\r\n/g, '\n')
                .replace(/\r/g, '\n')
                .replace(/\u00a0/g, ' ')
                .replace(/[ \t]+$/gm, '')
                .replace(/^\n+|\n+$/g, '')
                .replace(/\n{4,}/g, '\n\n\n');
            const blocks = [];
            const images = [];
            const imageIndexes = new Map();
            const imageElements = new Map();
            const excludedSelector = [
                'script', 'style', 'template', 'noscript', 'nav', '[role="navigation"]',
                'button', '[role="button"]', '[aria-hidden="true"]', '[hidden]',
                '.breadcrumbs', '.breadcrumb', '.sidebar', '[class*="sidebar"]',
                '.pagination', '[class*="pagination"]', '.page-footer', '.site-footer',
                '.mw-editsection', '.navbox', '.catlinks', '.printfooter', '.metadata',
                '.toc', '#toc', '[data-testid="table-of-contents"]',
                'a[rel="next"]', 'a[rel="prev"]', '[aria-label="Next"]', '[aria-label="Previous"]'
            ].join(',');
            const semanticSelector = 'h1,h2,h3,h4,h5,h6,p,pre,code,blockquote,ul,ol,table,figure,img,picture,[data-language],[data-code-block]';
            const pickLargest = selector => Array.from(document.querySelectorAll(selector))
                .filter(element => !element.matches('[hidden],[aria-hidden="true"]'))
                .sort((left, right) => cleanInline(right.innerText || right.textContent || '').length - cleanInline(left.innerText || left.textContent || '').length)[0] || null;
            const root = document.querySelector('#mw-content-text .mw-parser-output')
                || pickLargest('[itemprop="articleBody"], article[role="main"], main article, article, .main-page-content')
                || pickLargest('[role="main"], main')
                || document.body;

            const pushBlock = block => {
                if (block.kind !== 'image') {
                    block.text = block.kind === 'code' || block.kind === 'table'
                        ? cleanCode(block.text)
                        : cleanInline(block.text);
                    if (!block.text) return;
                }
                const previous = blocks[blocks.length - 1];
                if (previous
                    && previous.kind === block.kind
                    && previous.text === block.text
                    && previous.level === block.level
                    && previous.language === block.language
                    && previous.imageIndex === block.imageIndex) return;
                blocks.push(block);
            };
            const textWithoutNestedBlocks = element => {
                const clone = element.cloneNode(true);
                for (const nested of Array.from(clone.querySelectorAll(`ul,ol,pre,table,figure,${excludedSelector}`))) nested.remove();
                return cleanInline(clone.innerText || clone.textContent || '');
            };
            const appendText = (kind, element, level = 0) => {
                const clone = element.cloneNode(true);
                for (const excluded of Array.from(clone.querySelectorAll(excludedSelector))) excluded.remove();
                pushBlock({
                    kind,
                    text: clone.innerText || clone.textContent || '',
                    level,
                    language: '',
                    imageIndex: -1
                });
            };
            const appendImage = image => {
                if (!image || image.matches('[aria-hidden="true"],[role="presentation"]')) return;
                const source = resolveUrl(image.currentSrc
                    || image.getAttribute('data-src')
                    || image.getAttribute('data-original')
                    || image.getAttribute('data-lazy-src')
                    || image.getAttribute('src')
                    || '');
                if (!source) return;
                const bounds = image.getBoundingClientRect();
                const width = image.naturalWidth || Number(image.getAttribute('width')) || Math.round(bounds.width) || 0;
                const height = image.naturalHeight || Number(image.getAttribute('height')) || Math.round(bounds.height) || 0;
                const alt = cleanInline(image.getAttribute('alt') || image.getAttribute('title') || '');
                if (width > 0 && height > 0 && width < 48 && height < 48) return;
                if (!alt && width > 0 && height > 0 && width <= 64 && height <= 64) return;

                let imageIndex = imageIndexes.get(source);
                if (imageIndex === undefined) {
                    imageIndex = images.length;
                    imageIndexes.set(source, imageIndex);
                    imageElements.set(imageIndex, image);
                    images.push({
                        index: imageIndex,
                        source: resolveUrl(image.getAttribute('src') || ''),
                        currentSource: resolveUrl(image.currentSrc || ''),
                        sourceSet: image.getAttribute('srcset') || '',
                        alt,
                        title: cleanInline(image.getAttribute('title') || ''),
                        naturalWidth: width,
                        naturalHeight: height,
                        lazySource: resolveUrl(image.getAttribute('data-src') || image.getAttribute('data-original') || image.getAttribute('data-lazy-src') || ''),
                        lazySourceSet: image.getAttribute('data-srcset') || image.getAttribute('data-lazy-srcset') || ''
                    });
                }
                pushBlock({ kind: 'image', text: '', level: 0, language: '', imageIndex });
            };
            const findCodeLanguage = element => {
                const candidates = [element, element.querySelector('code'), element.closest('[data-language]'), element.parentElement]
                    .filter(Boolean);
                for (const candidate of candidates) {
                    const dataLanguage = candidate.getAttribute?.('data-language') || candidate.getAttribute?.('data-lang');
                    if (dataLanguage) return cleanInline(dataLanguage);
                    const classes = typeof candidate.className === 'string' ? candidate.className : '';
                    const match = classes.match(/(?:language-|lang-)([\w#+.-]+)/i) || classes.match(/brush:\s*([\w#+.-]+)/i);
                    if (match) return cleanInline(match[1]);
                }
                const label = element.closest('.code-example,.sandpack,.code-block')?.querySelector('[class*="language"],.visually-hidden');
                const labelText = cleanInline(label?.textContent || '');
                return /^[\w#+.-]{1,24}$/.test(labelText) ? labelText : '';
            };
            const appendCode = element => {
                const codeElement = element.matches('pre,code') ? element : element.querySelector('pre,code');
                if (!codeElement) return;
                const clone = codeElement.cloneNode(true);
                for (const excluded of Array.from(clone.querySelectorAll(`${excludedSelector},.line-number,.line-numbers,[data-line-numbers-col],[data-line-number]`))) excluded.remove();
                const content = clone.querySelector('[data-line-numbers-content]') || clone;
                const lineNodes = Array.from(content.querySelectorAll(':scope > .line,.line[data-line],.token-line'));
                const code = lineNodes.length > 1
                    ? lineNodes.map(line => line.textContent || '').join('\n')
                    : (codeElement.innerText || content.textContent || '');
                let language = findCodeLanguage(element);
                if (!language && /(?:^|\n)\s*(?:import|export|const|let|var|function|class)\b|<[A-Z][\w.]*(?:\s|\/?>)/m.test(code)) {
                    language = 'javascript';
                }
                pushBlock({
                    kind: 'code',
                    text: code,
                    level: 0,
                    language,
                    imageIndex: -1
                });
            };
            const isCodeContainer = element => element.matches('pre')
                || (element.matches('code')
                    && (cleanCode(element.textContent || '').includes('\n')
                        || getComputedStyle(element).display === 'block'
                        || /(?:^|[\s_-])(code-example|code-block|example-code)(?:$|[\s_-])/i.test(
                            typeof element.parentElement?.className === 'string' ? element.parentElement.className : '')))
                || (element.matches('[data-language],[data-code-block],.sandpack--codeblock,.sp-code-editor,.expressive-code')
                    && Boolean(element.querySelector('pre,code')));
            const appendList = list => {
                const kind = list.tagName.toLowerCase() === 'ol' ? 'numbered-list-item' : 'bulleted-list-item';
                for (const item of Array.from(list.querySelectorAll(':scope > li'))) {
                    pushBlock({ kind, text: textWithoutNestedBlocks(item), level: 0, language: '', imageIndex: -1 });
                    for (const nested of Array.from(item.querySelectorAll(':scope > ul,:scope > ol'))) appendList(nested);
                }
            };
            const appendTable = table => {
                for (const image of Array.from(table.querySelectorAll('img'))) appendImage(image);
                const rows = Array.from(table.querySelectorAll('tr'))
                    .map(row => Array.from(row.querySelectorAll(':scope > th,:scope > td'))
                        .map(cell => cleanInline(cell.innerText || cell.textContent || ''))
                        .filter(Boolean))
                    .filter(row => row.length > 0);
                if (rows.length > 0) {
                    pushBlock({
                        kind: 'table',
                        text: rows.map(row => row.join('\t')).join('\n'),
                        level: 0,
                        language: '',
                        imageIndex: -1
                    });
                }
            };
            const walk = element => {
                for (const child of Array.from(element.children)) {
                    if (child.matches(excludedSelector)) continue;
                    const tag = child.tagName.toLowerCase();
                    if (tag === 'img') { appendImage(child); continue; }
                    if (tag === 'picture') { appendImage(child.querySelector('img')); continue; }
                    if (tag === 'figure') {
                        for (const image of Array.from(child.querySelectorAll('img'))) appendImage(image);
                        const figureCodeBlocks = [
                            ...Array.from(child.querySelectorAll('pre')),
                            ...Array.from(child.querySelectorAll('code'))
                                .filter(code => !code.closest('pre') && isCodeContainer(code))
                        ];
                        for (const codeBlock of figureCodeBlocks) appendCode(codeBlock);
                        const caption = child.querySelector('figcaption');
                        if (caption) appendText('paragraph', caption);
                        continue;
                    }
                    if (isCodeContainer(child)) { appendCode(child); continue; }
                    if (/^h[1-6]$/.test(tag)) { appendText('heading', child, Number(tag.slice(1))); continue; }
                    if (tag === 'p') {
                        appendText('paragraph', child);
                        for (const image of Array.from(child.querySelectorAll('img'))) appendImage(image);
                        continue;
                    }
                    if (tag === 'blockquote') { appendText('quote', child); continue; }
                    if (tag === 'ul' || tag === 'ol') { appendList(child); continue; }
                    if (tag === 'table') { appendTable(child); continue; }
                    if (tag === 'hr') continue;
                    if (child.querySelector(semanticSelector)) {
                        walk(child);
                        continue;
                    }
                    appendText('paragraph', child);
                }
            };
            if (root) walk(root);

            await Promise.all(images.map(async image => {
                const source = image.currentSource || image.lazySource || image.source || '';
                if (!/(?:\.svg(?:[?#]|$)|^data:image\/svg\+xml)/i.test(source)) return;
                const element = imageElements.get(image.index);
                if (!element) return;
                try {
                    if (typeof element.decode === 'function') await element.decode();
                    const sourceWidth = element.naturalWidth || image.naturalWidth || 0;
                    const sourceHeight = element.naturalHeight || image.naturalHeight || 0;
                    if (sourceWidth <= 0 || sourceHeight <= 0) return;
                    const scale = Math.min(1, 1600 / sourceWidth, 1600 / sourceHeight);
                    const canvas = document.createElement('canvas');
                    canvas.width = Math.max(1, Math.round(sourceWidth * scale));
                    canvas.height = Math.max(1, Math.round(sourceHeight * scale));
                    const context = canvas.getContext('2d');
                    if (!context) return;
                    context.drawImage(element, 0, 0, canvas.width, canvas.height);
                    image.currentSource = canvas.toDataURL('image/png');
                    image.naturalWidth = canvas.width;
                    image.naturalHeight = canvas.height;
                } catch {
                    // Cross-origin SVGs remain source-link fallbacks.
                }
            }));

            const markdown = blocks.map(block => {
                if (block.kind === 'heading') return `${'#'.repeat(block.level)} ${block.text}`;
                if (block.kind === 'bulleted-list-item') return `- ${block.text}`;
                if (block.kind === 'numbered-list-item') return `1. ${block.text}`;
                if (block.kind === 'quote') return `> ${block.text}`;
                if (block.kind === 'code') return `\`\`\`${block.language}\n${block.text}\n\`\`\``;
                if (block.kind === 'image') return `COGNT-BROWSER-IMAGE-${block.imageIndex}`;
                return block.text;
            }).join('\n\n').trim();

            return {
                title: document.title || '',
                url: location.href || '',
                text: root ? String(root.innerText || root.textContent || '') : '',
                markdown,
                html: '',
                blocks,
                images
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
            .EvaluateScriptAsPromiseAsync(BrowserPageExtractionScript, TimeSpan.FromSeconds(45))
            .ConfigureAwait(true);
        if (!response.Success || response.Result is not string scriptResult)
        {
            throw new InvalidOperationException(response.Message ?? "The page extraction script failed.");
        }

        BrowserPageScriptResult page = JsonSerializer.Deserialize<BrowserPageScriptResult>(
            scriptResult,
            BrowserExtractionJsonOptions) ?? throw new InvalidOperationException("The page did not return extractable content.");

        string pageUrl = IsAllowedWebUri(page.Url) ? page.Url! : currentUrl!;
        IReadOnlyList<BrowserPageContentBlock> blocks = NormalizeExtractedBlocks(page.Blocks);
        IReadOnlyList<BrowserPageImageExtraction> images = NormalizeExtractedImages(page.Images);
        if (string.IsNullOrWhiteSpace(page.Text)
            && string.IsNullOrWhiteSpace(page.Markdown)
            && string.IsNullOrWhiteSpace(page.Html)
            && blocks.Count == 0
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
            blocks,
            images,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<BrowserPageContentBlock> NormalizeExtractedBlocks(
        IReadOnlyList<BrowserPageScriptBlockResult>? blocks)
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
        foreach (BrowserPageScriptBlockResult block in blocks)
        {
            string kind = NormalizeExtractedSingleLine(block.Kind).ToLowerInvariant();
            if (!allowedKinds.Contains(kind))
            {
                continue;
            }

            string text = kind is BrowserPageContentBlockKinds.Code or BrowserPageContentBlockKinds.Table
                ? NormalizeExtractedMultiline(block.Text)
                : NormalizeExtractedSingleLine(block.Text);
            int imageIndex = kind == BrowserPageContentBlockKinds.Image ? block.ImageIndex : -1;
            if (kind != BrowserPageContentBlockKinds.Image && string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            normalized.Add(new BrowserPageContentBlock(
                kind,
                text,
                kind == BrowserPageContentBlockKinds.Heading ? Math.Clamp(block.Level, 1, 6) : 0,
                kind == BrowserPageContentBlockKinds.Code ? NormalizeExtractedSingleLine(block.Language) : string.Empty,
                imageIndex));
        }

        return normalized;
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
        IReadOnlyList<BrowserPageScriptBlockResult>? Blocks,
        IReadOnlyList<BrowserPageScriptImageResult>? Images);

    private sealed record BrowserPageScriptBlockResult(
        string? Kind,
        string? Text,
        int Level,
        string? Language,
        int ImageIndex);

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
