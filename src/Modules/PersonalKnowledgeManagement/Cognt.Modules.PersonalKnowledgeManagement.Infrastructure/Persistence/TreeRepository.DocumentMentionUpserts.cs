using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.ReplaceDocumentMentions(
        string sourceDocumentId,
        IReadOnlyList<DocumentMentionRow> mentions)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            string normalizedSourceDocumentId = NormalizeDocumentId(sourceDocumentId);

            using (SqliteCommand deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = DeleteDocumentMentionsBySourceSql;
                AddDeleteDocumentMentionsBySourceParameters(deleteCommand, normalizedSourceDocumentId);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (DocumentMentionRow mention in mentions)
            {
                using SqliteCommand insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = InsertDocumentMentionSql;
                AddDocumentMentionInsertParameters(insertCommand, normalizedSourceDocumentId, mention);
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        });
    }
}
