using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Shell.Browser;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal async Task<string> CreateBrowserPageNoteAsync(BrowserPageExtraction extraction)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before extracting browser content.");
        }

        BrowserPageExtraction normalized = NormalizeBrowserExtraction(extraction);
        SaveCurrentNoteFromEditor();

        KnowledgeCreatedNoteReadModel createdNote = await Task.Run(() =>
        {
            string content = documentContentFactory.FromMarkdown(
                CreateBrowserPageMarkdown(normalized),
                normalized.Title);
            return noteService.CreateNote(openCollectionPath, parentId: null, normalized.Title, content);
        });

        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
        return createdNote.RelativePath;
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
            Html = string.IsNullOrWhiteSpace(extraction.Html) ? string.Empty : extraction.Html.Replace('\0', ' ').Trim(),
            Images = NormalizeBrowserImages(extraction.Images),
        };
    }

    private static string CreateBrowserPageMarkdown(BrowserPageExtraction extraction)
    {
        StringBuilder markdown = new();
        markdown.Append("Source: <").Append(extraction.Url).AppendLine(">");
        markdown.Append("Extracted: ")
            .AppendLine(extraction.ExtractedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"));
        markdown.AppendLine();

        string body = string.IsNullOrWhiteSpace(extraction.Markdown)
            ? extraction.Text
            : extraction.Markdown;
        if (!string.IsNullOrWhiteSpace(body))
        {
            markdown.AppendLine("## Page content").AppendLine();
            markdown.AppendLine(body).AppendLine();
        }

        if (extraction.Images.Count > 0)
        {
            markdown.AppendLine("## Images").AppendLine();
            foreach (BrowserPageImageExtraction image in extraction.Images)
            {
                AppendBrowserImageMarkdown(markdown, image);
            }

            markdown.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(extraction.Html))
        {
            string fence = CreateMarkdownCodeFence(extraction.Html);
            markdown.AppendLine("## Source HTML").AppendLine();
            markdown.Append(fence).AppendLine("html");
            markdown.AppendLine(extraction.Html);
            markdown.AppendLine(fence);
        }

        return markdown.ToString();
    }

    private static void AppendBrowserImageMarkdown(StringBuilder markdown, BrowserPageImageExtraction image)
    {
        string label = string.IsNullOrWhiteSpace(image.Alt) ? $"Image {image.Index + 1}" : image.Alt;
        string primarySource = FirstNonEmpty(image.CurrentSource, image.Source, image.LazySource);
        markdown.Append("- ");
        if (string.IsNullOrWhiteSpace(primarySource))
        {
            markdown.AppendLine(label);
        }
        else
        {
            markdown.Append("![").Append(label.Replace("]", "\\]", StringComparison.Ordinal)).Append("](").Append(primarySource).AppendLine(")");
        }

        AppendBrowserImageValue(markdown, "Source", image.Source);
        AppendBrowserImageValue(markdown, "Current source", image.CurrentSource);
        AppendBrowserImageValue(markdown, "Source set", image.SourceSet);
        AppendBrowserImageValue(markdown, "Lazy source", image.LazySource);
        AppendBrowserImageValue(markdown, "Lazy source set", image.LazySourceSet);
        AppendBrowserImageValue(markdown, "Title", image.Title);
        if (image.NaturalWidth > 0 || image.NaturalHeight > 0)
        {
            markdown.Append("  - Size: ").Append(image.NaturalWidth).Append(" x ").Append(image.NaturalHeight).AppendLine();
        }
    }

    private static void AppendBrowserImageValue(StringBuilder markdown, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            markdown.Append("  - ").Append(label).Append(": ").AppendLine(value);
        }
    }

    private static IReadOnlyList<BrowserPageImageExtraction> NormalizeBrowserImages(IReadOnlyList<BrowserPageImageExtraction>? images)
    {
        if (images is null || images.Count == 0)
        {
            return [];
        }

        List<BrowserPageImageExtraction> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (BrowserPageImageExtraction image in images)
        {
            string source = NormalizeBrowserSingleLine(image.Source);
            string currentSource = NormalizeBrowserSingleLine(image.CurrentSource);
            string lazySource = NormalizeBrowserSingleLine(image.LazySource);
            string identity = string.Join("|", source, currentSource, lazySource, image.SourceSet, image.LazySourceSet);
            if (string.IsNullOrWhiteSpace(identity.Replace("|", string.Empty, StringComparison.Ordinal)) || !seen.Add(identity))
            {
                continue;
            }

            normalized.Add(image with
            {
                Source = source,
                CurrentSource = currentSource,
                SourceSet = NormalizeBrowserSingleLine(image.SourceSet),
                Alt = NormalizeBrowserSingleLine(image.Alt),
                Title = NormalizeBrowserSingleLine(image.Title),
                NaturalWidth = Math.Max(0, image.NaturalWidth),
                NaturalHeight = Math.Max(0, image.NaturalHeight),
                LazySource = lazySource,
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

    private static string CreateMarkdownCodeFence(string content)
    {
        int longestRun = 0;
        int currentRun = 0;
        foreach (char character in content)
        {
            if (character == '`')
            {
                longestRun = Math.Max(longestRun, ++currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
