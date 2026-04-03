using System.Text.Json;

namespace CogntApp.Services;

public static class BlockSuiteDocumentContent
{
    public const string Kind = "cognt-blocksuite-doc";
    public const int Version = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Empty()
    {
        return FromPlainText(string.Empty);
    }

    public static string FromPlainText(string text, string title = "")
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        string normalizedText = text ?? string.Empty;
        string pageId = NewBlockId();
        string surfaceId = NewBlockId();
        string noteId = NewBlockId();
        string paragraphId = NewBlockId();

        var snapshot = new
        {
            kind = Kind,
            version = Version,
            snapshot = new
            {
                type = "page",
                meta = new
                {
                    id = pageId,
                    title = normalizedTitle,
                    createDate = now,
                    tags = Array.Empty<string>()
                },
                blocks = new
                {
                    type = "block",
                    id = pageId,
                    flavour = "affine:page",
                    version = 2,
                    props = new Dictionary<string, object?>
                    {
                        ["title"] = TextValue(normalizedTitle)
                    },
                    children = new object[]
                    {
                        new
                        {
                            type = "block",
                            id = surfaceId,
                            flavour = "affine:surface",
                            version = 5,
                            props = new Dictionary<string, object?>(),
                            children = Array.Empty<object>()
                        },
                        new
                        {
                            type = "block",
                            id = noteId,
                            flavour = "affine:note",
                            version = 1,
                            props = new Dictionary<string, object?>
                            {
                                ["xywh"] = "[0,0,800,95]",
                                ["background"] = "--affine-note-background-blue",
                                ["index"] = "a0",
                                ["hidden"] = false,
                                ["displayMode"] = "both",
                                ["edgeless"] = new Dictionary<string, object?>
                                {
                                    ["style"] = new Dictionary<string, object?>
                                    {
                                        ["borderRadius"] = 8,
                                        ["borderSize"] = 4,
                                        ["borderStyle"] = "none",
                                        ["shadowType"] = "--affine-note-shadow-box"
                                    }
                                }
                            },
                            children = new object[]
                            {
                                new
                                {
                                    type = "block",
                                    id = paragraphId,
                                    flavour = "affine:paragraph",
                                    version = 1,
                                    props = new Dictionary<string, object?>
                                    {
                                        ["type"] = "text",
                                        ["text"] = TextValue(normalizedText),
                                        ["collapsed"] = false
                                    },
                                    children = Array.Empty<object>()
                                }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine;
    }

    private static Dictionary<string, object?> TextValue(string value)
    {
        object[] delta = string.IsNullOrEmpty(value)
            ? Array.Empty<object>()
            : new object[] { new Dictionary<string, string> { ["insert"] = value } };

        return new Dictionary<string, object?>
        {
            ["$blocksuite:internal:text$"] = true,
            ["delta"] = delta
        };
    }

    private static string NewBlockId()
    {
        return "cognt-" + Guid.NewGuid().ToString("N")[..12];
    }
}
