using System.Text.Json;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static class BlockSuiteDocumentText
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    internal static string FromPlainText(string text, string title = "")
    {
        object snapshot = BlockSuiteDocumentSnapshotFactory.Create(text, title);
        return JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine;
    }

    internal static string ToPlainText(string content)
    {
        return BlockSuitePlainTextReader.Read(content);
    }
}
