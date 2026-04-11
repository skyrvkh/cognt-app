namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentContent
{
    private static object CreateParagraphBlock(BlockSuiteDocumentBlock block)
    {
        Dictionary<string, object?> props = new()
        {
            ["type"] = block.Type,
            ["text"] = TextValue(block.Text),
            ["collapsed"] = false
        };

        if (block.Checked.HasValue)
        {
            props["checked"] = block.Checked.Value;
        }

        if (!string.IsNullOrWhiteSpace(block.Language))
        {
            props["language"] = block.Language;
        }

        return new
        {
            type = "block",
            id = NewBlockId(),
            flavour = "affine:paragraph",
            version = 1,
            props,
            children = Array.Empty<object>()
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
            ["delta"] = delta
        };
    }

    private static string NewBlockId()
    {
        return "cognt-" + Guid.NewGuid().ToString("N")[..12];
    }
}
