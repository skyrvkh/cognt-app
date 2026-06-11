namespace CogntApp.Shell.Browser;

internal sealed record BrowserPageExtraction(
    string Title,
    string Url,
    string Text,
    string Markdown,
    string Html,
    IReadOnlyList<BrowserPageImageExtraction> Images,
    DateTimeOffset ExtractedUtc);

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
