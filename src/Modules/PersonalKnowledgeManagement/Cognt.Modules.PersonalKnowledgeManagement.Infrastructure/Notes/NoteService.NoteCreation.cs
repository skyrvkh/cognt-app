using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    public KnowledgeCreatedNoteReadModel CreateNote(
        string collectionId,
        long? parentId,
        string title,
        string initialContent,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        return CreateNoteInCollectionRoot(
            collectionId,
            parentId,
            root => BuildCreatedNoteDraft(root, title, initialContent),
            previousSiblingId,
            nextSiblingId);
    }

    public KnowledgeCreatedNoteReadModel CreateExtractNote(
        string collectionId,
        long? parentId,
        string sourceRelativePath,
        string initialContent,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        return CreateNoteInCollectionRoot(
            collectionId,
            parentId,
            root => BuildExtractNoteDraft(root, sourceRelativePath, initialContent),
            previousSiblingId,
            nextSiblingId);
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

        TryWriteNewFile(notePath, EnsureTrailingNewline(initialContent));

        try
        {
            long itemId = repository.CreateNote(parentId, title, fileRelPath, previousSiblingId, nextSiblingId);
            KnowledgeTreeItemRow createdItem = repository.GetItem(itemId)
                ?? throw new InvalidOperationException("The created note tree item could not be loaded.");
            InvalidateNoteCatalogCache(root);
            return new KnowledgeCreatedNoteReadModel(createdItem.Id, createdItem.ParentId, fileRelPath, createdItem.Title);
        }
        catch
        {
            TryDeleteFile(notePath);
            throw;
        }
    }
}
