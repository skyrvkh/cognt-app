using System.Text.Json.Nodes;
using CogntApp.Shell.Browser;
using Xunit;

namespace Cognt.Wpf.Tests;

public sealed class BrowserPageDocumentBuilderTests
{
    [Fact]
    public void Build_CreatesNativeBlocksInExtractedDocumentOrder()
    {
        BrowserPageExtraction extraction = CreateExtraction(
        [
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.Heading, "Example", 2, string.Empty, -1),
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.Code, "const value = 1;", 0, "javascript", -1),
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.Image, string.Empty, 0, string.Empty, 4),
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.BulletedListItem, "First item", 0, string.Empty, -1),
        ],
        [CreateImage(4)]);
        Dictionary<int, BrowserPageAttachedImage> attachedImages = new()
        {
            [4] = new BrowserPageAttachedImage(
                4,
                "Layout diagram",
                "attachments\\images\\layout.png",
                321,
                640,
                480),
        };

        JsonArray children = GetNoteChildren(BrowserPageDocumentBuilder.Build(extraction, attachedImages));

        Assert.Equal("affine:paragraph", children[3]!["flavour"]!.GetValue<string>());
        Assert.Equal("h2", children[3]!["props"]!["type"]!.GetValue<string>());
        Assert.Equal("affine:code", children[4]!["flavour"]!.GetValue<string>());
        Assert.Equal("javascript", children[4]!["props"]!["language"]!.GetValue<string>());
        Assert.Equal("const value = 1;", GetText(children[4]!));
        Assert.Equal("affine:image", children[5]!["flavour"]!.GetValue<string>());
        Assert.Equal("attachments/images/layout.png", children[5]!["props"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(640, children[5]!["props"]!["width"]!.GetValue<int>());
        Assert.Equal("affine:list", children[6]!["flavour"]!.GetValue<string>());
        Assert.Equal("bulleted", children[6]!["props"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Build_PreservesAnImageReferenceWhenAttachmentDownloadFails()
    {
        BrowserPageExtraction extraction = CreateExtraction(
            [new BrowserPageContentBlock(BrowserPageContentBlockKinds.Image, string.Empty, 0, string.Empty, 2)],
            [CreateImage(2)]);

        JsonArray children = GetNoteChildren(BrowserPageDocumentBuilder.Build(
            extraction,
            new Dictionary<int, BrowserPageAttachedImage>()));

        Assert.Equal("affine:paragraph", children[3]!["flavour"]!.GetValue<string>());
        Assert.Equal(
            "Reference image: https://example.test/reference.png",
            GetText(children[3]!));
    }

    [Fact]
    public void Build_CreatesNativeNumberedListAndQuoteBlocks()
    {
        BrowserPageExtraction extraction = CreateExtraction(
        [
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.NumberedListItem, "Step one", 0, string.Empty, -1),
            new BrowserPageContentBlock(BrowserPageContentBlockKinds.Quote, "Important", 0, string.Empty, -1),
        ],
        []);

        JsonArray children = GetNoteChildren(BrowserPageDocumentBuilder.Build(
            extraction,
            new Dictionary<int, BrowserPageAttachedImage>()));

        Assert.Equal("affine:list", children[3]!["flavour"]!.GetValue<string>());
        Assert.Equal("numbered", children[3]!["props"]!["type"]!.GetValue<string>());
        Assert.Equal("affine:paragraph", children[4]!["flavour"]!.GetValue<string>());
        Assert.Equal("quote", children[4]!["props"]!["type"]!.GetValue<string>());
    }

    private static BrowserPageExtraction CreateExtraction(
        IReadOnlyList<BrowserPageContentBlock> blocks,
        IReadOnlyList<BrowserPageImageExtraction> images)
    {
        return new BrowserPageExtraction(
            "Reference page",
            "https://example.test/reference",
            string.Empty,
            string.Empty,
            string.Empty,
            blocks,
            images,
            new DateTimeOffset(2026, 7, 12, 4, 5, 6, TimeSpan.Zero));
    }

    private static BrowserPageImageExtraction CreateImage(int index)
    {
        return new BrowserPageImageExtraction(
            index,
            "https://example.test/reference.png",
            "https://example.test/reference.png",
            string.Empty,
            "Reference image",
            string.Empty,
            640,
            480,
            string.Empty,
            string.Empty);
    }

    private static JsonArray GetNoteChildren(string content)
        => JsonNode.Parse(content)!["snapshot"]!["blocks"]!["children"]![0]!["children"]!.AsArray();

    private static string GetText(JsonNode block)
        => block["props"]!["text"]!["delta"]![0]!["insert"]!.GetValue<string>();
}
