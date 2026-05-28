using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void CollectBlockSuiteMentionRows(
        string root,
        DocumentLinkNote source,
        JsonObject block,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath,
        DocumentLinkTitleIndex titleIndex,
        List<DocumentMentionRow> rows,
        long now,
        ILogger<NoteService>? logger = null)
    {
        string sourceBlockId = GetStableBlockSuiteBlockId(block);
        DocumentMentionBuilder builder = new(root, source, sourceBlockId, notesByPath, logger);
        JsonObject? props = block["props"] as JsonObject;
        string? blockTarget = GetLinkedNoteRelativePathFromReference(block, logger) ?? GetLinkedNoteRelativePathFromReference(props, logger);
        string? blockTitle = props is null ? null : GetTrimmedJsonStringProperty(props, "title");
        if (!string.IsNullOrWhiteSpace(blockTarget))
        {
            builder.AppendReference(blockTarget, blockTitle, ExplicitLinkMentionType);
        }

        if (props is not null)
        {
            AppendBlockSuiteRichTextMentions(props["title"], builder, logger);
            AppendBlockSuiteRichTextMentions(props["text"], builder, logger);
        }

        rows.AddRange(MaterializeDocumentMentionRows(builder, source, titleIndex, now, logger));

        if (block["children"] is not JsonArray children)
        {
            return;
        }

        foreach (JsonNode? child in children)
        {
            if (child is JsonObject childBlock)
            {
                CollectBlockSuiteMentionRows(
                    root,
                    source,
                    childBlock,
                    notesByPath,
                    titleIndex,
                    rows,
                    now,
                    logger);
            }
        }
    }

    private static void CollectPlainTextMentionRows(
        string root,
        DocumentLinkNote source,
        string sourceBlockId,
        string text,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath,
        DocumentLinkTitleIndex titleIndex,
        List<DocumentMentionRow> rows,
        long now,
        ILogger<NoteService>? logger = null)
    {
        DocumentMentionBuilder builder = new(root, source, sourceBlockId, notesByPath, logger);
        builder.AppendText(text);
        rows.AddRange(MaterializeDocumentMentionRows(builder, source, titleIndex, now, logger));
    }
}
