using System.Text.Json;
using System.Text.RegularExpressions;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static readonly Regex AttachmentReferenceRegex = new(
        @"attachments(?:/images)?/[^""'\\<>\s\)\]\}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private void SyncSavedNoteAttachmentReferences(
        string root,
        string normalizedRelativePath,
        string content)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        KnowledgeTreeItemRow? note = repository.FindNoteByFileRelPath(normalizedRelativePath);
        if (note is null)
        {
            return;
        }

        SyncNoteAttachmentReferences(repository, note, content);
    }

    private static void SyncNoteAttachmentReferences(
        ITreeRepository repository,
        KnowledgeTreeItemRow note,
        string content)
    {
        IReadOnlyList<AttachmentRow> attachments = repository.LoadAttachments();
        Dictionary<string, AttachmentRow> attachmentsById = new(attachments.Count, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AttachmentRow> imageAttachmentsBySource = new(StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentRow attachment in attachments)
        {
            attachmentsById.TryAdd(attachment.Id, attachment);
            if (!IsImageAttachment(attachment))
            {
                continue;
            }

            AddImageAttachmentSource(imageAttachmentsBySource, attachment.RelativePath, attachment);
            AddImageAttachmentSource(imageAttachmentsBySource, attachment.Id, attachment);
            AddImageAttachmentSource(imageAttachmentsBySource, attachment.Sha256, attachment);
            AddImageAttachmentSource(imageAttachmentsBySource, attachment.StoredFileName, attachment);
        }

        IReadOnlyList<NoteAttachmentRow> existingReferences = repository.LoadNoteAttachments(note.Id);
        Dictionary<string, NoteAttachmentRow> existingByAttachmentId = new(existingReferences.Count, StringComparer.OrdinalIgnoreCase);
        foreach (NoteAttachmentRow reference in existingReferences)
        {
            existingByAttachmentId.TryAdd(reference.AttachmentId, reference);
        }

        IReadOnlyList<string> referencedImageIds = ExtractReferencedImageAttachmentIds(
            content,
            imageAttachmentsBySource);
        List<NoteAttachmentRow> replacementReferences = new(existingReferences.Count + referencedImageIds.Count);
        HashSet<string> addedAttachmentIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (NoteAttachmentRow reference in existingReferences)
        {
            if (!attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment)
                || IsImageAttachment(attachment)
                || !addedAttachmentIds.Add(reference.AttachmentId))
            {
                continue;
            }

            replacementReferences.Add(reference);
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (string attachmentId in referencedImageIds)
        {
            if (!attachmentsById.TryGetValue(attachmentId, out AttachmentRow? attachment)
                || !addedAttachmentIds.Add(attachmentId))
            {
                continue;
            }

            long createdAt = existingByAttachmentId.TryGetValue(attachmentId, out NoteAttachmentRow? existingReference)
                ? existingReference.CreatedAt
                : now;
            string displayName = existingReference?.DisplayName ?? attachment.OriginalFileName;
            replacementReferences.Add(new NoteAttachmentRow(
                CreateNoteAttachmentId(note.Uid, attachment.Id),
                note.Id,
                note.Uid,
                attachment.Id,
                displayName,
                createdAt));
        }

        if (NoteAttachmentReferencesMatch(existingReferences, replacementReferences))
        {
            return;
        }

        repository.ReplaceNoteAttachments(note.Id, replacementReferences);
    }

    private static bool NoteAttachmentReferencesMatch(
        IReadOnlyList<NoteAttachmentRow> existingReferences,
        IReadOnlyList<NoteAttachmentRow> replacementReferences)
    {
        if (existingReferences.Count != replacementReferences.Count)
        {
            return false;
        }

        Dictionary<string, NoteAttachmentRow> existingByAttachmentId = new(existingReferences.Count, StringComparer.OrdinalIgnoreCase);
        foreach (NoteAttachmentRow existingReference in existingReferences)
        {
            if (!existingByAttachmentId.TryAdd(existingReference.AttachmentId, existingReference))
            {
                return false;
            }
        }

        foreach (NoteAttachmentRow replacementReference in replacementReferences)
        {
            if (!existingByAttachmentId.TryGetValue(replacementReference.AttachmentId, out NoteAttachmentRow? existingReference)
                || !string.Equals(existingReference.Id, replacementReference.Id, StringComparison.OrdinalIgnoreCase)
                || existingReference.NoteItemId != replacementReference.NoteItemId
                || !string.Equals(existingReference.NoteDocumentId, replacementReference.NoteDocumentId, StringComparison.Ordinal)
                || !string.Equals(existingReference.DisplayName, replacementReference.DisplayName, StringComparison.Ordinal)
                || existingReference.CreatedAt != replacementReference.CreatedAt)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddImageAttachmentSource(
        Dictionary<string, AttachmentRow> imageAttachmentsBySource,
        string source,
        AttachmentRow attachment)
    {
        string normalizedSource = NormalizeAttachmentReferenceToken(source);
        if (normalizedSource.Length > 0)
        {
            imageAttachmentsBySource.TryAdd(normalizedSource, attachment);
        }
    }

    private static IReadOnlyList<string> ExtractReferencedImageAttachmentIds(
        string content,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource)
    {
        List<string> referencedImageIds = [];
        HashSet<string> seenAttachmentIds = new(StringComparer.OrdinalIgnoreCase);
        AddImageReferencesFromText(content, imageAttachmentsBySource, referencedImageIds, seenAttachmentIds);
        if (content.Contains("\\/", StringComparison.Ordinal))
        {
            AddImageReferencesFromText(
                content.Replace("\\/", "/", StringComparison.Ordinal),
                imageAttachmentsBySource,
                referencedImageIds,
                seenAttachmentIds);
        }

        AddImageReferencesFromJson(content, imageAttachmentsBySource, referencedImageIds, seenAttachmentIds);
        return referencedImageIds;
    }

    private static void AddImageReferencesFromText(
        string content,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource,
        List<string> referencedImageIds,
        HashSet<string> seenAttachmentIds)
    {
        foreach (Match match in AttachmentReferenceRegex.Matches(content))
        {
            TryAddImageAttachmentReference(
                match.Value,
                imageAttachmentsBySource,
                referencedImageIds,
                seenAttachmentIds);
        }
    }

    private static void AddImageReferencesFromJson(
        string content,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource,
        List<string> referencedImageIds,
        HashSet<string> seenAttachmentIds)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            AddImageReferencesFromJsonElement(
                document.RootElement,
                imageAttachmentsBySource,
                referencedImageIds,
                seenAttachmentIds);
        }
        catch (JsonException)
        {
        }
    }

    private static void AddImageReferencesFromJsonElement(
        JsonElement element,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource,
        List<string> referencedImageIds,
        HashSet<string> seenAttachmentIds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                string? value = element.GetString();
                if (value is not null)
                {
                    TryAddImageAttachmentReference(
                        value,
                        imageAttachmentsBySource,
                        referencedImageIds,
                        seenAttachmentIds);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AddImageReferencesFromJsonElement(
                        item,
                        imageAttachmentsBySource,
                        referencedImageIds,
                        seenAttachmentIds);
                }

                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AddImageReferencesFromJsonElement(
                        property.Value,
                        imageAttachmentsBySource,
                        referencedImageIds,
                        seenAttachmentIds);
                }

                break;
        }
    }

    private static void TryAddImageAttachmentReference(
        string source,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource,
        List<string> referencedImageIds,
        HashSet<string> seenAttachmentIds)
    {
        if (!TryResolveImageAttachmentSource(source, imageAttachmentsBySource, out AttachmentRow? attachment)
            || attachment is null)
        {
            return;
        }

        if (seenAttachmentIds.Add(attachment.Id))
        {
            referencedImageIds.Add(attachment.Id);
        }
    }

    private static bool TryResolveImageAttachmentSource(
        string source,
        IReadOnlyDictionary<string, AttachmentRow> imageAttachmentsBySource,
        out AttachmentRow? attachment)
    {
        attachment = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        string trimmedSource = source.Trim();
        if (imageAttachmentsBySource.TryGetValue(trimmedSource, out attachment))
        {
            return true;
        }

        if (!MayContainAttachmentReference(trimmedSource))
        {
            return false;
        }

        string normalizedSource = NormalizeAttachmentReferenceToken(trimmedSource);
        if (imageAttachmentsBySource.TryGetValue(normalizedSource, out attachment))
        {
            return true;
        }

        string? decodedSha256Source = TryDecodeSha256SourceToken(normalizedSource);
        return decodedSha256Source is not null
            && imageAttachmentsBySource.TryGetValue(decodedSha256Source, out attachment);
    }

    private static bool MayContainAttachmentReference(string source)
    {
        return source.Length <= 256
            || source.Contains("attachments/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("attachments\\", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("att-", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAttachmentReferenceToken(string source)
    {
        string normalizedSource = NormalizeImageSourceSafely(source);
        int queryStart = normalizedSource.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
        {
            normalizedSource = normalizedSource[..queryStart];
        }

        return NormalizeRelativePath(normalizedSource);
    }

    private static string NormalizeImageSourceSafely(string source)
    {
        try
        {
            return NormalizeImageSource(source);
        }
        catch (UriFormatException)
        {
            return source.Replace('\\', '/')
                .Trim()
                .Trim('<', '>')
                .TrimStart('/');
        }
    }
}
