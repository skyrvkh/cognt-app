namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;

public enum KnowledgeAttachmentKind
{
    Pdf = 1,
    Document = 2,
    Image = 3,
}

public sealed record KnowledgeAttachmentReadModel(
    string AttachmentId,
    string OriginalFileName,
    string RelativePath,
    KnowledgeAttachmentKind Kind);
