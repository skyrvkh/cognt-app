using System.IO;
using System.Text;
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
        IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId,
        IReadOnlyDictionary<long, IReadOnlyList<string>> assignedTagsByItemId)
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
        IReadOnlyList<string> contentTags = ExtractNoteTags(notePath, content);
        IReadOnlyList<string> assignedTags = assignedTagsByItemId.TryGetValue(row.Id, out IReadOnlyList<string>? existingTags)
            ? existingTags
            : [];
        IReadOnlyList<string> tags = MergeTags(assignedTags, contentTags);
        string searchText = NormalizeSearchText(title, relativePath, pathText, plainText, tags);

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
        return assignedTags
            .Concat(contentTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => SearchableDocumentText.NormalizeWhitespace(tag))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
}
