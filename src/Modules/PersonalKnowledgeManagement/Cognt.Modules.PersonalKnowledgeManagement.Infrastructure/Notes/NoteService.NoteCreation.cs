using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    KnowledgeCreatedNoteReadModel INoteService.CreateNote(
        string collectionId,
        long? parentId,
        string title,
        string initialContent,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        KnowledgeCreatedNoteReadModel createdNote = CreateNoteInCollectionRoot(
            collectionId,
            parentId,
            root => BuildCreatedNoteDraft(root, title, initialContent),
            previousSiblingId,
            nextSiblingId);
        return createdNote;
    }

    KnowledgeCreatedNoteReadModel INoteService.CreateExtractNote(
        string collectionId,
        long? parentId,
        string sourceRelativePath,
        string initialContent,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        string normalizedSourceRelativePath = NormalizeRelativePath(sourceRelativePath);

        KnowledgeCreatedNoteReadModel createdNote = CreateNoteInCollectionRoot(
            collectionId,
            parentId,
            root => BuildExtractNoteDraft(root, normalizedSourceRelativePath, initialContent),
            previousSiblingId,
            nextSiblingId);
        return createdNote;
    }

    private KnowledgeCreatedNoteReadModel CreateNoteInCollectionRoot(
        string collectionId,
        long? parentId,
        Func<string, PendingNoteCreation> buildNoteDraft,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ArgumentNullException.ThrowIfNull(buildNoteDraft);

        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        PendingNoteCreation noteDraft = buildNoteDraft(root);
        return PersistCreatedNote(
            root,
            repository,
            parentId,
            noteDraft.Title,
            noteDraft.Content,
            noteDraft.RelativePath,
            previousSiblingId,
            nextSiblingId);
    }

    private KnowledgeCreatedNoteReadModel PersistCreatedNote(
        string root,
        ITreeRepository repository,
        long? parentId,
        string title,
        string initialContent,
        string fileRelPath,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        string notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        Directory.CreateDirectory(Path.GetDirectoryName(notePath)
            ?? throw new InvalidOperationException("The note file does not have a parent directory."));

        bool fileCreated = TryWriteNewFile(notePath, EnsureTrailingNewline(initialContent), logger);
        if (!fileCreated)
        {
            logger?.LogWarning(
                "Note creation continuing after new file write was skipped because the target file already exists. Root={Root} RelativePath={RelativePath} NotePath={NotePath}",
                root,
                fileRelPath,
                notePath);
        }

        try
        {
            long itemId = repository.CreateNote(parentId, title, fileRelPath, previousSiblingId, nextSiblingId);
            KnowledgeTreeItemRow createdItem = repository.GetItem(itemId)
                ?? throw new InvalidOperationException("The created note tree item could not be loaded.");
            InvalidateNoteCatalogCache(root);
            return new KnowledgeCreatedNoteReadModel(createdItem.Id, createdItem.ParentId, fileRelPath, createdItem.Title);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Created note persistence failed after file write; attempting file rollback. Root={Root} RelativePath={RelativePath} NotePath={NotePath} ParentId={ParentId} Title={Title}",
                root,
                fileRelPath,
                notePath,
                parentId,
                title);
            TryDeleteFile(notePath, logger);
            throw;
        }
    }
}
