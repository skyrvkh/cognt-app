using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

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
        long now)
    {
        string sourceBlockId = GetStableBlockSuiteBlockId(block);
        DocumentMentionBuilder builder = new(root, source, sourceBlockId, notesByPath);
        JsonObject? props = block["props"] as JsonObject;
        string? blockTarget = GetLinkedNoteRelativePathFromReference(block) ?? GetLinkedNoteRelativePathFromReference(props);
        string? blockTitle = props is null ? null : GetTrimmedJsonStringProperty(props, "title");
        if (!string.IsNullOrWhiteSpace(blockTarget))
        {
            builder.AppendReference(blockTarget, blockTitle, ExplicitLinkMentionType);
        }

        if (props is not null)
        {
            AppendBlockSuiteRichTextMentions(props["title"], builder);
            AppendBlockSuiteRichTextMentions(props["text"], builder);
        }

        rows.AddRange(MaterializeDocumentMentionRows(builder, source, titleIndex, now));

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
                    now);
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
        long now)
    {
        DocumentMentionBuilder builder = new(root, source, sourceBlockId, notesByPath);
        builder.AppendText(text);
        rows.AddRange(MaterializeDocumentMentionRows(builder, source, titleIndex, now));
    }
}
