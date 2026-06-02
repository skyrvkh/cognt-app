using System.Globalization;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static class AllNotesPresentationFormatter
{
    internal static string FormatTimestamp(DateTimeOffset value)
    {
        return value
            .ToLocalTime()
            .ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
    }

    internal static string GetTypeLabel(KnowledgeNoteListItemType type)
    {
        return type switch
        {
            KnowledgeNoteListItemType.Markdown => "Markdown",
            KnowledgeNoteListItemType.PdfLinkedNote => "PDF-linked note",
            KnowledgeNoteListItemType.AttachmentBackedNote => "Attachment-backed note",
            _ => "Note"
        };
    }

    internal static int CompareNoteTypes(
        KnowledgeNoteListItemType left,
        KnowledgeNoteListItemType right)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(GetTypeLabel(left), GetTypeLabel(right));
    }

    internal static string FormatCardCount(int cardCount)
    {
        return cardCount == 0
            ? string.Empty
            : cardCount.ToString(CultureInfo.InvariantCulture);
    }

    internal static string NormalizeSourceRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }

    internal static string? NormalizeOptionalRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? null
            : NormalizeSourceRelativePath(relativePath);
    }

    internal static string CreateTagsLabel(IReadOnlyCollection<string> tags)
    {
        return tags.Count == 0 ? string.Empty : string.Join(", ", tags);
    }

    internal static string GetTreeFallbackTypeLabel(KnowledgeItemKind kind)
    {
        return kind switch
        {
            KnowledgeItemKind.Folder => "Folder",
            KnowledgeItemKind.PdfDocument => "PDF",
            KnowledgeItemKind.Trash => "Trash",
            KnowledgeItemKind.Root => "Workspace",
            _ => "Note"
        };
    }
}
