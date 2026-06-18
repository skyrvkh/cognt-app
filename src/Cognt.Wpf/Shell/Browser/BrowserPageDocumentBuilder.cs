using System.Text.Json;

namespace CogntApp.Shell.Browser;

internal sealed record BrowserPageAttachedImage(
    int ImageIndex,
    string Caption,
    string RelativePath,
    long Size,
    int Width,
    int Height);

internal static class BrowserPageDocumentBuilder
{
    private const string DocumentKind = "cognt-blocksuite-doc";
    private const int DocumentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    internal static string Build(
        BrowserPageExtraction extraction,
        IReadOnlyDictionary<int, BrowserPageAttachedImage> attachedImages)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(attachedImages);

        List<object> children =
        [
            CreateParagraphBlock("text", $"Source: {extraction.Url}"),
            CreateParagraphBlock(
                "text",
                $"Extracted: {extraction.ExtractedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}"),
            CreateParagraphBlock("h2", "Page content"),
        ];

        if (extraction.Blocks.Count == 0)
        {
            string fallback = string.IsNullOrWhiteSpace(extraction.Text)
                ? extraction.Markdown
                : extraction.Text;
            children.Add(CreateParagraphBlock("text", fallback));
        }
        else
        {
            foreach (BrowserPageContentBlock block in extraction.Blocks)
            {
                AppendContentBlock(children, block, extraction.Images, attachedImages);
            }
        }

        string title = string.IsNullOrWhiteSpace(extraction.Title)
            ? "Browser page"
            : extraction.Title.Trim();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string pageId = CreateBlockId();
        string noteId = CreateBlockId();

        var document = new
        {
            kind = DocumentKind,
            version = DocumentVersion,
            snapshot = new
            {
                type = "page",
                meta = new
                {
                    id = pageId,
                    title,
                    createDate = now,
                    tags = Array.Empty<string>(),
                },
                blocks = new
                {
                    type = "block",
                    id = pageId,
                    flavour = "affine:page",
                    version = 2,
                    props = new Dictionary<string, object?>
                    {
                        ["title"] = CreateTextValue(title),
                    },
                    children = new object[]
                    {
                        new
                        {
                            type = "block",
                            id = noteId,
                            flavour = "affine:note",
                            version = 1,
                            props = new Dictionary<string, object?>
                            {
                                ["xywh"] = "[0,0,498,92]",
                                ["background"] = "--affine-note-background-white",
                                ["index"] = "a0",
                                ["lockedBySelf"] = false,
                                ["hidden"] = false,
                                ["displayMode"] = "both",
                                ["edgeless"] = new Dictionary<string, object?>
                                {
                                    ["style"] = new Dictionary<string, object?>
                                    {
                                        ["borderRadius"] = 8,
                                        ["borderSize"] = 4,
                                        ["borderStyle"] = "none",
                                        ["shadowType"] = "--affine-note-shadow-box",
                                    },
                                },
                            },
                            children = children.ToArray(),
                        },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    private static void AppendContentBlock(
        List<object> children,
        BrowserPageContentBlock block,
        IReadOnlyList<BrowserPageImageExtraction> images,
        IReadOnlyDictionary<int, BrowserPageAttachedImage> attachedImages)
    {
        switch (block.Kind)
        {
            case BrowserPageContentBlockKinds.Heading:
                children.Add(CreateParagraphBlock($"h{Math.Clamp(block.Level, 1, 6)}", block.Text));
                break;

            case BrowserPageContentBlockKinds.BulletedListItem:
                children.Add(CreateListBlock("bulleted", block.Text));
                break;

            case BrowserPageContentBlockKinds.NumberedListItem:
                children.Add(CreateListBlock("numbered", block.Text));
                break;

            case BrowserPageContentBlockKinds.Quote:
                children.Add(CreateParagraphBlock("quote", block.Text));
                break;

            case BrowserPageContentBlockKinds.Code:
                children.Add(CreateCodeBlock(block.Text, block.Language));
                break;

            case BrowserPageContentBlockKinds.Table:
                children.Add(CreateParagraphBlock("text", block.Text));
                break;

            case BrowserPageContentBlockKinds.Image:
                AppendImageBlock(children, block.ImageIndex, images, attachedImages);
                break;

            default:
                children.Add(CreateParagraphBlock("text", block.Text));
                break;
        }
    }

    private static void AppendImageBlock(
        List<object> children,
        int imageIndex,
        IReadOnlyList<BrowserPageImageExtraction> images,
        IReadOnlyDictionary<int, BrowserPageAttachedImage> attachedImages)
    {
        if (attachedImages.TryGetValue(imageIndex, out BrowserPageAttachedImage? image))
        {
            children.Add(CreateImageBlock(image));
            return;
        }

        BrowserPageImageExtraction? source = images.FirstOrDefault(image => image.Index == imageIndex);
        if (source is null)
        {
            return;
        }

        string label = string.IsNullOrWhiteSpace(source.Alt)
            ? $"Image {imageIndex + 1}"
            : source.Alt;
        string url = FirstNonEmpty(source.CurrentSource, source.LazySource, source.Source);
        children.Add(CreateParagraphBlock(
            "text",
            string.IsNullOrWhiteSpace(url) ? label : $"{label}: {url}"));
    }

    private static object CreateParagraphBlock(string paragraphType, string text)
    {
        return new
        {
            type = "block",
            id = CreateBlockId(),
            flavour = "affine:paragraph",
            version = 1,
            props = new Dictionary<string, object?>
            {
                ["type"] = paragraphType,
                ["text"] = CreateTextValue(text),
                ["collapsed"] = false,
            },
            children = Array.Empty<object>(),
        };
    }

    private static object CreateListBlock(string listType, string text)
    {
        return new
        {
            type = "block",
            id = CreateBlockId(),
            flavour = "affine:list",
            version = 1,
            props = new Dictionary<string, object?>
            {
                ["type"] = listType,
                ["text"] = CreateTextValue(text),
                ["checked"] = false,
                ["collapsed"] = false,
                ["order"] = null,
            },
            children = Array.Empty<object>(),
        };
    }

    private static object CreateCodeBlock(string text, string language)
    {
        return new
        {
            type = "block",
            id = CreateBlockId(),
            flavour = "affine:code",
            version = 1,
            props = new Dictionary<string, object?>
            {
                ["text"] = CreateTextValue(text),
                ["language"] = string.IsNullOrWhiteSpace(language) ? "Plain Text" : language,
                ["wrap"] = false,
                ["caption"] = string.Empty,
            },
            children = Array.Empty<object>(),
        };
    }

    private static object CreateImageBlock(BrowserPageAttachedImage image)
    {
        return new
        {
            type = "block",
            id = CreateBlockId(),
            flavour = "affine:image",
            version = 1,
            props = new Dictionary<string, object?>
            {
                ["caption"] = image.Caption,
                ["sourceId"] = image.RelativePath.Replace('\\', '/'),
                ["width"] = image.Width,
                ["height"] = image.Height,
                ["index"] = "a0",
                ["xywh"] = "[0,0,0,0]",
                ["lockedBySelf"] = false,
                ["rotate"] = 0,
                ["size"] = image.Size,
            },
            children = Array.Empty<object>(),
        };
    }

    private static Dictionary<string, object?> CreateTextValue(string? value)
    {
        string text = value ?? string.Empty;
        object[] delta = text.Length == 0
            ? Array.Empty<object>()
            : [new Dictionary<string, string> { ["insert"] = text }];
        return new Dictionary<string, object?>
        {
            ["$blocksuite:internal:text$"] = true,
            ["delta"] = delta,
        };
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string CreateBlockId()
        => "cognt-" + Guid.NewGuid().ToString("N")[..12];
}
