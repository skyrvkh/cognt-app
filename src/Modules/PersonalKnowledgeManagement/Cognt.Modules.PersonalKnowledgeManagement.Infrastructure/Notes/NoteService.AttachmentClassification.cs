using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static AttachmentClassification ClassifyAttachment(string path)
    {
        string extension = NormalizeAttachmentExtension(Path.GetExtension(path));
        if (string.Equals(extension, PdfExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new AttachmentClassification(KnowledgeAttachmentKind.Pdf, "application/pdf");
        }

        if (ImageAttachmentExtensions.Contains(extension))
        {
            return new AttachmentClassification(KnowledgeAttachmentKind.Image, GetImageContentType(extension));
        }

        if (DocumentAttachmentExtensions.Contains(extension))
        {
            return new AttachmentClassification(KnowledgeAttachmentKind.Document, GetDocumentContentType(extension));
        }

        throw new InvalidOperationException(
            $"Unsupported attachment type \"{extension}\". Supported types: PDF, DOCX, DOC, RTF, TXT, MD, MARKDOWN, PNG, JPG, JPEG, WEBP, GIF.");
    }

    private static KnowledgeAttachmentKind InferAttachmentKind(string extension, string contentType)
    {
        string normalizedExtension = NormalizeAttachmentExtension(extension);
        if (string.Equals(normalizedExtension, PdfExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return KnowledgeAttachmentKind.Pdf;
        }

        return ImageAttachmentExtensions.Contains(normalizedExtension)
            || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? KnowledgeAttachmentKind.Image
                : KnowledgeAttachmentKind.Document;
    }

    private static string GetDocumentContentType(string extension)
    {
        return NormalizeAttachmentExtension(extension).ToLowerInvariant() switch
        {
            DocExtension => "application/msword",
            DocxExtension => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            MarkdownNoteExtension or MarkdownImportExtension => "text/markdown",
            RtfExtension => "application/rtf",
            TextExtension => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static string GetImageContentType(string extension)
    {
        return NormalizeAttachmentExtension(extension).ToLowerInvariant() switch
        {
            PngExtension => "image/png",
            JpgExtension or JpegExtension => "image/jpeg",
            WebpExtension => "image/webp",
            GifExtension => "image/gif",
            _ => "application/octet-stream"
        };
    }

    private sealed class AttachmentClassification
    {
        internal AttachmentClassification(KnowledgeAttachmentKind kind, string contentType)
        {
            Kind = kind;
            ContentType = contentType;
        }

        internal KnowledgeAttachmentKind Kind { get; }

        internal string ContentType { get; }
    }
}
