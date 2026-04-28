using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static KnowledgePdfDocumentReadModel ToPdfReadModel(KnowledgeAttachmentReadModel attachment)
    {
        return new KnowledgePdfDocumentReadModel(
            attachment.RelativePath,
            Path.GetFileNameWithoutExtension(attachment.OriginalFileName));
    }

    private void EnsureImportedAttachmentTreeState(
        ITreeRepository repository,
        KnowledgeAttachmentReadModel attachment,
        long? parentId,
        string root)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(attachment);

        if (attachment.Kind == KnowledgeAttachmentKind.Pdf)
        {
            AttachmentRow row = repository.GetAttachment(attachment.AttachmentId)
                ?? throw new InvalidOperationException("The imported PDF could not be found in attachment storage.");
            EnsurePdfAttachmentTreeItem(repository, row, parentId, preserveDeletedTreeItems: false);
        }
    }
}
