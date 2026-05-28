using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.ReplaceDocumentMentions(
        string sourceDocumentId,
        IReadOnlyList<DocumentMentionRow> mentions)
    {
        string normalizedSourceDocumentId = NormalizeDocumentId(sourceDocumentId);
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            using (SqliteCommand deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = DeleteDocumentMentionsBySourceSql;
                AddDeleteDocumentMentionsBySourceParameters(deleteCommand, normalizedSourceDocumentId);
                deleteCommand.ExecuteNonQuery();
            }

            int insertedCount = 0;
            int resolvedCount = 0;
            foreach (DocumentMentionRow mention in mentions)
            {
                using SqliteCommand insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = InsertDocumentMentionSql;
                AddDocumentMentionInsertParameters(insertCommand, normalizedSourceDocumentId, mention);
                insertCommand.ExecuteNonQuery();
                insertedCount++;
                if (mention.TargetDocumentId is not null)
                {
                    resolvedCount++;
                }
            }

            transaction.Commit();
        });
    }
}
