using Cognt.Modules.PersonalKnowledgeManagement.Domain;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal interface ITreeRepository : IDisposable
{
    long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreateNote(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreatePdfDocument(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    void RenameItem(long itemId, string title);

    void DeleteItem(long itemId);
}
