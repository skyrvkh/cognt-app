using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static NoteCatalogEntry? TryCreateNoteCatalogEntry(
        string root,
        KnowledgeTreeItemRow row,
        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId,
        IReadOnlyDictionary<long, IReadOnlyList<string>> assignedTagsByItemId,
        ILogger<NoteService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(row.FileRelPath))
        {
            logger?.LogWarning(
                "Catalog entry skipped because note row has no file path. NoteItemId={NoteItemId} Title={Title}",
                row.Id,
                row.Title);
            return null;
        }

        string relativePath = NormalizeRelativePath(row.FileRelPath);
        string notePath = Path.Combine(root, ToPlatformPath(relativePath));
        if (!File.Exists(notePath))
        {
            logger?.LogWarning(
                "Catalog entry skipped because note file is missing. NoteItemId={NoteItemId} RelativePath={RelativePath} NotePath={NotePath}",
                row.Id,
                relativePath,
                notePath);
            return null;
        }

        string? content = TryReadCatalogNoteContent(notePath, row.Id, relativePath, logger);
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
        IReadOnlyList<string> contentTags = ExtractNoteTags(notePath, content, logger);
        IReadOnlyList<string> assignedTags = assignedTagsByItemId.TryGetValue(row.Id, out IReadOnlyList<string>? existingTags)
            ? existingTags
            : [];
        IReadOnlyList<string> tags = MergeTags(assignedTags, contentTags);
        string searchText = NormalizeSearchText(title, relativePath, pathText, plainText, tags, logger);

        return new NoteCatalogEntry(
            new KnowledgeNoteListItemReadModel(
                row.Id,
                title,
                relativePath,
                ClassifyNoteListItemType(notePath, noteAttachments),
                tags,
                DateTimeOffset.FromUnixTimeSeconds(row.CreatedAt),
                DateTimeOffset.FromUnixTimeSeconds(row.UpdatedAt),
                assignedTags,
                contentTags),
            searchText);
    }

    private static IReadOnlyList<string> MergeTags(
        IReadOnlyList<string> assignedTags,
        IReadOnlyList<string> contentTags)
    {
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> tags = new(assignedTags.Count + contentTags.Count);
        AddCatalogTags(assignedTags, seenTags, tags);
        AddCatalogTags(contentTags, seenTags, tags);

        tags.Sort(StringComparer.OrdinalIgnoreCase);
        return tags.ToArray();
    }

    private static void AddCatalogTags(
        IReadOnlyList<string> source,
        HashSet<string> seenTags,
        List<string> target)
    {
        foreach (string tag in source)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            string normalizedTag = SearchableDocumentText.NormalizeWhitespace(tag);
            if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
            {
                target.Add(normalizedTag);
            }
        }
    }

    private static string? TryReadCatalogNoteContent(
        string notePath,
        long noteItemId,
        string relativePath,
        ILogger<NoteService>? logger = null)
    {
        try
        {
            return File.ReadAllText(notePath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException)
        {
            logger?.LogWarning(
                ex,
                "Catalog entry skipped because note content could not be read. NoteItemId={NoteItemId} RelativePath={RelativePath} NotePath={NotePath}",
                noteItemId,
                relativePath,
                notePath);
            return null;
        }
    }
}
