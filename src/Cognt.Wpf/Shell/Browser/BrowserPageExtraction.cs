namespace CogntApp.Shell.Browser;

internal sealed record BrowserPageExtraction(
    string Title,
    string Url,
    string Text,
    string Markdown,
    string Html,
    IReadOnlyList<BrowserPageContentBlock> Blocks,
    IReadOnlyList<BrowserPageImageExtraction> Images,
    DateTimeOffset ExtractedUtc);

internal sealed record BrowserPageContentBlock(
    string Kind,
    string Text,
    int Level,
    string Language,
    int ImageIndex);

internal static class BrowserPageContentBlockKinds
{
    internal const string Paragraph = "paragraph";
    internal const string Heading = "heading";
    internal const string BulletedListItem = "bulleted-list-item";
    internal const string NumberedListItem = "numbered-list-item";
    internal const string Quote = "quote";
    internal const string Code = "code";
    internal const string Table = "table";
    internal const string Image = "image";
}

internal sealed record BrowserPageImageExtraction(
    int Index,
    string Source,
    string CurrentSource,
    string SourceSet,
    string Alt,
    string Title,
    int NaturalWidth,
    int NaturalHeight,
    string LazySource,
    string LazySourceSet);
