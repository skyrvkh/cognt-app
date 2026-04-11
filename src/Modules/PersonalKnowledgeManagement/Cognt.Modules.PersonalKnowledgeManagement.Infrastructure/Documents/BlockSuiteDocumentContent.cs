using System.Text.Json;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentContent
{
    internal const string Kind = "cognt-blocksuite-doc";
    internal const int Version = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    internal static string FromPlainText(string text, string title)
    {
        return FromBlocks(
            [BlockSuiteDocumentBlock.PlainText(text ?? string.Empty)],
            title);
    }

    internal static string FromBlocks(
        IReadOnlyList<BlockSuiteDocumentBlock> blocks,
        string title)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        string pageId = NewBlockId();
        string noteId = NewBlockId();
        object[] children = blocks.Count == 0
            ? [CreateParagraphBlock(BlockSuiteDocumentBlock.PlainText(string.Empty))]
            : blocks.Select(CreateParagraphBlock).ToArray();

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
                                        ["shadowType"] = "--affine-note-shadow-box"
                                    }
                                }
                            },
                            children
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine;
    }
}

