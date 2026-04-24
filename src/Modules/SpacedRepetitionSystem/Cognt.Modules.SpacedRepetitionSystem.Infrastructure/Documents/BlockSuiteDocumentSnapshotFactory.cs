namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentSnapshotFactory
{
    private const string Kind = "cognt-blocksuite-doc";
    private const int Version = 1;

    internal static object Create(string text, string title)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        string normalizedText = text ?? string.Empty;
        string pageId = NewBlockId();

        return new
        {
            kind = Kind,
            version = Version,
            snapshot = CreateSnapshot(pageId, normalizedTitle, normalizedText, now),
        };
    }

    private static Dictionary<string, object?> TextValue(string value)
    {
        object[] delta = string.IsNullOrEmpty(value)
            ? Array.Empty<object>()
            : [new Dictionary<string, string> { ["insert"] = value }];

        return new Dictionary<string, object?>
        {
            ["$blocksuite:internal:text$"] = true,
            ["delta"] = delta,
        };
    }

    private static string NewBlockId()
    {
        return "cognt-" + Guid.NewGuid().ToString("N")[..12];
    }
}
