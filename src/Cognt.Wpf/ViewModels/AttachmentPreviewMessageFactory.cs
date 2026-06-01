using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;

namespace CogntApp.ViewModels;

internal static class AttachmentPreviewMessageFactory
{
    internal static string CreateUnavailablePreviewMessage(KnowledgeAttachmentReadModel attachment)
    {
        string previewKind = attachment.Kind == KnowledgeAttachmentKind.Image
            ? "Image"
            : "Document";

        return $"{previewKind} preview is not available yet.{Environment.NewLine}{Environment.NewLine}File: {attachment.OriginalFileName}{Environment.NewLine}Stored at: {attachment.RelativePath}";
    }
}
