namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentSnapshotFactory
{
    private static object CreateSnapshot(
        string pageId,
        string title,
        string text,
        long createdAt)
    {
        return new
        {
            type = "page",
            meta = new
            {
                id = pageId,
                title,
                createDate = createdAt,
                tags = Array.Empty<string>(),
            },
            blocks = CreatePageBlock(pageId, title, text),
        };
    }

    private static object CreatePageBlock(string pageId, string title, string text)
    {
        return new
        {
            type = "block",
            id = pageId,
            flavour = "affine:page",
            version = 2,
            props = new Dictionary<string, object?>
            {
                ["title"] = TextValue(title),
            },
            children = new[]
            {
                CreateSurfaceBlock(NewBlockId()),
                CreateNoteBlock(NewBlockId(), text),
            },
        };
    }

    private static object CreateSurfaceBlock(string surfaceId)
    {
        return new
        {
            type = "block",
            id = surfaceId,
            flavour = "affine:surface",
            version = 5,
            props = new Dictionary<string, object?>(),
            children = Array.Empty<object>(),
        };
    }

    private static object CreateNoteBlock(string noteId, string text)
    {
        return new
        {
            type = "block",
            id = noteId,
            flavour = "affine:note",
            version = 1,
            props = CreateNoteProperties(),
            children = new[]
            {
                CreateParagraphBlock(NewBlockId(), text),
            },
        };
    }

    private static object CreateParagraphBlock(string paragraphId, string text)
    {
        return new
        {
            type = "block",
            id = paragraphId,
            flavour = "affine:paragraph",
            version = 1,
            props = new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = TextValue(text),
                ["collapsed"] = false,
            },
            children = Array.Empty<object>(),
        };
    }

    private static Dictionary<string, object?> CreateNoteProperties()
    {
        return new Dictionary<string, object?>
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
                    ["shadowType"] = "--affine-note-shadow-box",
                },
            },
        };
    }
}
