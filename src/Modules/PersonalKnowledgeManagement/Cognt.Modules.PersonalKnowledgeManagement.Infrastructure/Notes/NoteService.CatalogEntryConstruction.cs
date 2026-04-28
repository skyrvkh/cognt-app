using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> LoadCatalogAttachmentsByNoteId(
        ITreeRepository repository)
    {
        IReadOnlyDictionary<string, AttachmentRow> attachmentsById = repository
            .LoadAttachments()
            .ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);

        return repository
            .LoadNoteAttachments()
            .GroupBy(row => row.NoteItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AttachmentRow>)group
                    .Select(reference => attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment) ? attachment : null)
                    .Where(attachment => attachment is not null)
                    .Cast<AttachmentRow>()
                    .ToArray());
    }

    private static NoteCatalogEntry? TryCreateNoteCatalogEntry(
        string root,
        KnowledgeTreeItemRow row,
        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId)
    {
        if (string.IsNullOrWhiteSpace(row.FileRelPath))
        {
            return null;
        }

        string relativePath = NormalizeRelativePath(row.FileRelPath);
        string notePath = Path.Combine(root, ToPlatformPath(relativePath));
        if (!File.Exists(notePath))
        {
            return null;
        }

        string? content = TryReadCatalogNoteContent(notePath);
        if (content is null)
        {
            return null;
        }

        IReadOnlyList<AttachmentRow> noteAttachments = attachmentsByNoteId.TryGetValue(row.Id, out IReadOnlyList<AttachmentRow>? attached)
            ? attached
            : [];
        string title = string.IsNullOrWhiteSpace(row.Title)
            ? Path.GetFileNameWithoutExtension(notePath)
            : row.Title;
        string plainText = SearchableDocumentText.ExtractPlainText(notePath, content);
        string pathText = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
        IReadOnlyList<string> tags = ExtractNoteTags(notePath, content);
        string searchText = NormalizeSearchText(title, relativePath, pathText, plainText, tags);

        return new NoteCatalogEntry(
            new KnowledgeNoteListItemReadModel(
                title,
                relativePath,
                ClassifyNoteListItemType(notePath, noteAttachments),
                tags,
                DateTimeOffset.FromUnixTimeSeconds(row.CreatedAt),
                DateTimeOffset.FromUnixTimeSeconds(row.UpdatedAt)),
            searchText);
    }

    private static string? TryReadCatalogNoteContent(string notePath)
    {
        try
        {
            return File.ReadAllText(notePath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractNoteTags(string notePath, string content)
    {
        if (!string.Equals(Path.GetExtension(notePath), BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            JsonNode? root = JsonNode.Parse(content);
            HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);
            CollectSnapshotMetaTags(root?["snapshot"]?["meta"]?["tags"], tags);
            CollectPropertyTagValuesRecursively(root, tags);
            return tags
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void CollectSnapshotMetaTags(JsonNode? node, ISet<string> tags)
    {
        if (node is not JsonArray array)
        {
            return;
        }

        foreach (JsonNode? item in array)
        {
            AddNormalizedTagValues(item?.GetValue<string>(), tags);
        }
    }

    private static void CollectPropertyTagValuesRecursively(JsonNode? node, ISet<string> tags)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.TryGetPropertyValue("name", out JsonNode? nameNode)
                    && obj.TryGetPropertyValue("value", out JsonNode? valueNode)
                    && string.Equals(nameNode?.GetValue<string>()?.Trim(), "tags", StringComparison.OrdinalIgnoreCase))
                {
                    AddNormalizedTagValues(valueNode?.GetValue<string>(), tags);
                }

                foreach (KeyValuePair<string, JsonNode?> property in obj)
                {
                    CollectPropertyTagValuesRecursively(property.Value, tags);
                }

                break;

            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    CollectPropertyTagValuesRecursively(item, tags);
                }

                break;
        }
    }

    private static void AddNormalizedTagValues(string? value, ISet<string> tags)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (string tag in value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = SearchableDocumentText.NormalizeWhitespace(tag);
            if (normalized.Length > 0)
            {
                tags.Add(normalized);
            }
        }
    }

    private static KnowledgeNoteListItemType ClassifyNoteListItemType(
        string notePath,
        IReadOnlyList<AttachmentRow> attachments)
    {
        if (attachments.Any(attachment => InferAttachmentKind(attachment.FileExtension, attachment.ContentType) == KnowledgeAttachmentKind.Pdf))
        {
            return KnowledgeNoteListItemType.PdfLinkedNote;
        }

        if (attachments.Count > 0)
        {
            return KnowledgeNoteListItemType.AttachmentBackedNote;
        }

        string extension = Path.GetExtension(notePath);
        return string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownImportExtension, StringComparison.OrdinalIgnoreCase)
                ? KnowledgeNoteListItemType.Markdown
                : KnowledgeNoteListItemType.Note;
    }
}
