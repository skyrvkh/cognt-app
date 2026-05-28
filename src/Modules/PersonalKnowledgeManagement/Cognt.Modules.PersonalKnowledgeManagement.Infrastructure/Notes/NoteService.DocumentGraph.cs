using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    KnowledgeGraphReadModel INoteService.ReadDocumentGraph(
        string collectionId,
        string currentRelativePath,
        string currentContent,
        KnowledgeGraphSettingsReadModel settings)
    {
        KnowledgeGraphSettingsReadModel normalizedSettings = (settings ?? KnowledgeGraphSettingsReadModel.Default).Normalize();
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedCurrentRelativePath = NormalizeRelativePath(currentRelativePath);

        _ = ResolveExistingRelativeNotePathInRoot(root, normalizedCurrentRelativePath);

        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        Dictionary<string, DocumentLinkNote> notesByPath = LoadDocumentLinkNotesByRelativePath(root, repository, logger);
        if (!notesByPath.TryGetValue(normalizedCurrentRelativePath, out DocumentLinkNote? currentNote))
        {
            return KnowledgeGraphReadModel.Empty(normalizedSettings);
        }

        EnsureDocumentMentionIndexCurrent(
            root,
            repository,
            currentNote,
            currentContent,
            notesByPath,
            logger);

        Dictionary<string, DocumentLinkNote> notesByDocumentId = CreateNotesByDocumentId(notesByPath);
        KnowledgeGraphReadModel graph = KnowledgeGraphBuilder.Build(
            currentNote,
            normalizedSettings,
            notesByDocumentId,
            repository.LoadResolvedDocumentMentionsByDocument);

        return graph;
    }

    KnowledgeGraphSettingsReadModel INoteService.LoadGraphViewSettings(string collectionId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        return repository.LoadGraphViewSettings().Normalize();
    }

    void INoteService.SaveGraphViewSettings(
        string collectionId,
        KnowledgeGraphSettingsReadModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        KnowledgeGraphSettingsReadModel normalizedSettings = settings.Normalize();

        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        repository.SaveGraphViewSettings(normalizedSettings);

    }

    private static Dictionary<string, DocumentLinkNote> CreateNotesByDocumentId(
        Dictionary<string, DocumentLinkNote> notesByPath)
    {
        Dictionary<string, DocumentLinkNote> notesByDocumentId = new(notesByPath.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DocumentLinkNote note in notesByPath.Values)
        {
            notesByDocumentId.Add(note.DocumentId, note);
        }

        return notesByDocumentId;
    }
}
