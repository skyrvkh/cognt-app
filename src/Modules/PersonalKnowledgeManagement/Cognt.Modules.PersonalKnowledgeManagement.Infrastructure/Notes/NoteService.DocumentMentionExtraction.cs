using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<DocumentMentionRow> ExtractDocumentMentions(
        string root,
        DocumentLinkNote source,
        string content,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath,
        ILogger<NoteService>? logger = null)
    {

        DocumentLinkTitleIndex titleIndex = BuildDocumentLinkTitleIndex(notesByPath.Values, logger);
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
                        now,
                        logger);
                    return DocumentMentionRows.DistinctById(rows);
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
            now,
            logger);
        return DocumentMentionRows.DistinctById(rows);
    }
}
