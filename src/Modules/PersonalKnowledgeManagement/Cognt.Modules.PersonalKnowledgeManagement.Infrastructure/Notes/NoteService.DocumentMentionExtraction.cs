using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<DocumentMentionRow> ExtractDocumentMentions(
        string root,
        DocumentLinkNote source,
        string content,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath)
    {
        DocumentLinkTitleIndex titleIndex = BuildDocumentLinkTitleIndex(notesByPath.Values);
        List<DocumentMentionRow> rows = [];
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (IsBlockSuiteDocumentContent(content))
        {
            try
            {
                JsonNode? rootNode = JsonNode.Parse(content);
                JsonObject? block = GetBlockSuiteSnapshotBlocksRoot(rootNode);
                if (block is not null)
                {
                    CollectBlockSuiteMentionRows(
                        root,
                        source,
                        block,
                        notesByPath,
                        titleIndex,
                        rows,
                        now);
                    return MaterializeDistinctMentionRows(rows);
                }
            }
            catch (JsonException)
            {
                // Fall through to plain-text indexing for malformed document JSON.
            }
        }

        CollectPlainTextMentionRows(
            root,
            source,
            DocumentMentionBlockId,
            content,
            notesByPath,
            titleIndex,
            rows,
            now);
        return MaterializeDistinctMentionRows(rows);
    }

    private static DocumentMentionRow[] MaterializeDistinctMentionRows(IReadOnlyList<DocumentMentionRow> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        List<DocumentMentionRow> distinctRows = new(rows.Count);
        foreach (DocumentMentionRow row in rows)
        {
            if (seenIds.Add(row.Id))
            {
                distinctRows.Add(row);
            }
        }

        return distinctRows.ToArray();
    }
}
