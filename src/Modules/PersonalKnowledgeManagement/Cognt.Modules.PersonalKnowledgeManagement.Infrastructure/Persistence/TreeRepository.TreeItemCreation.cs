using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    long ITreeRepository.CreateFolder(long? parentId, string title, long? previousSiblingId, long? nextSiblingId)
    {
        return CreatePositionedTreeItem(
            parentId,
            KnowledgeTreeItemKind.Folder,
            title,
            fileRelPath: null,
            previousSiblingId,
            nextSiblingId);
    }

    long ITreeRepository.CreateNote(
        long? parentId,
        string title,
        string fileRelPath,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        return CreatePositionedTreeItem(
            parentId,
            KnowledgeTreeItemKind.Note,
            title,
            fileRelPath,
            previousSiblingId,
            nextSiblingId);
    }

    long ITreeRepository.CreatePdfDocument(
        long? parentId,
        string title,
        string fileRelPath,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        return CreatePositionedTreeItem(
            parentId,
            KnowledgeTreeItemKind.PdfDocument,
            title,
            fileRelPath,
            previousSiblingId,
            nextSiblingId);
    }

    private long CreatePositionedTreeItem(
        long? parentId,
        KnowledgeTreeItemKind kind,
        string title,
        string? fileRelPath,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        if (kind != KnowledgeTreeItemKind.Folder && string.IsNullOrWhiteSpace(fileRelPath))
        {
            logger?.LogWarning(
                "Rejected tree item creation because file path is required. Kind={Kind} ParentId={ParentId} Title={Title}",
                kind,
                parentId,
                title);
            throw new ArgumentException("File relative path is required.", nameof(fileRelPath));
        }

        string? normalizedFileRelPath = fileRelPath is null ? null : NormalizeFileRelPath(fileRelPath);
        return UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            EnsureValidParent(parentId, connection, transaction, logger);
            long now = GetCurrentUnixTimeSeconds();
            long position = AllocatePosition(parentId, previousSiblingId, nextSiblingId, connection, transaction);
            long id = InsertTreeItemRecord(
                parentId,
                kind,
                title,
                position,
                normalizedFileRelPath,
                now,
                connection,
                transaction);

            RecalculateChildCount(parentId, connection, transaction);
            transaction.Commit();
            return id;
        });
    }
}
