namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed class DocumentMentionRow
{
    internal DocumentMentionRow(
        string id,
        string sourceDocumentId,
        string sourceBlockId,
        string? targetDocumentId,
        string mentionType,
        string mentionText,
        string normalizedMentionText,
        int startOffset,
        int endOffset,
        string previewText,
        string? contextBefore,
        string? contextAfter,
        string status,
        long createdAt,
        long updatedAt)
    {
        Id = id;
        SourceDocumentId = sourceDocumentId;
        SourceBlockId = sourceBlockId;
        TargetDocumentId = targetDocumentId;
        MentionType = mentionType;
        MentionText = mentionText;
        NormalizedMentionText = normalizedMentionText;
        StartOffset = startOffset;
        EndOffset = endOffset;
        PreviewText = previewText;
        ContextBefore = contextBefore;
        ContextAfter = contextAfter;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    internal string Id { get; }

    internal string SourceDocumentId { get; }

    internal string SourceBlockId { get; }

    internal string? TargetDocumentId { get; }

    internal string MentionType { get; }

    internal string MentionText { get; }

    internal string NormalizedMentionText { get; }

    internal int StartOffset { get; }

    internal int EndOffset { get; }

    internal string PreviewText { get; }

    internal string? ContextBefore { get; }

    internal string? ContextAfter { get; }

    internal string Status { get; }

    internal long CreatedAt { get; }

    internal long UpdatedAt { get; }
}

internal sealed class AttachmentRow
{
    internal AttachmentRow(
        string id,
        string sha256,
        string originalFileName,
        string storedFileName,
        string relativePath,
        string contentType,
        string fileExtension,
        long sizeBytes,
        long createdAt,
        long lastReferencedAt)
    {
        Id = id;
        Sha256 = sha256;
        OriginalFileName = originalFileName;
        StoredFileName = storedFileName;
        RelativePath = relativePath;
        ContentType = contentType;
        FileExtension = fileExtension;
        SizeBytes = sizeBytes;
        CreatedAt = createdAt;
        LastReferencedAt = lastReferencedAt;
    }

    internal string Id { get; }

    internal string Sha256 { get; }

    internal string OriginalFileName { get; }

    internal string StoredFileName { get; }

    internal string RelativePath { get; }

    internal string ContentType { get; }

    internal string FileExtension { get; }

    internal long SizeBytes { get; }

    internal long CreatedAt { get; }

    internal long LastReferencedAt { get; }
}

internal sealed class NoteAttachmentRow
{
    internal NoteAttachmentRow(
        string id,
        long noteItemId,
        string noteDocumentId,
        string attachmentId,
        string displayName,
        long createdAt)
    {
        Id = id;
        NoteItemId = noteItemId;
        NoteDocumentId = noteDocumentId;
        AttachmentId = attachmentId;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    internal string Id { get; }

    internal long NoteItemId { get; }

    internal string NoteDocumentId { get; }

    internal string AttachmentId { get; }

    internal string DisplayName { get; }

    internal long CreatedAt { get; }
}
