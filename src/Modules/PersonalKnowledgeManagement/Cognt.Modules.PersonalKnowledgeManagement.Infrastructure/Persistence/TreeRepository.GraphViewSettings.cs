using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    KnowledgeGraphSettingsReadModel ITreeRepository.LoadGraphViewSettings()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT neighbor_depth,
                       max_nodes
                FROM graph_view_settings
                WHERE id = 1;
                """;

            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return KnowledgeGraphSettingsReadModel.Default;
            }

            return new KnowledgeGraphSettingsReadModel(
                reader.GetInt32(0),
                reader.GetInt32(1)).Normalize();
        });
    }

    void ITreeRepository.SaveGraphViewSettings(KnowledgeGraphSettingsReadModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        UseConnection(connection =>
        {
            KnowledgeGraphSettingsReadModel normalized = settings.Normalize();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO graph_view_settings (
                    id, neighbor_depth, max_nodes, updated_at
                )
                VALUES (
                    1, $neighborDepth, $maxNodes, $updatedAt
                )
                ON CONFLICT(id) DO UPDATE SET
                    neighbor_depth = excluded.neighbor_depth,
                    max_nodes = excluded.max_nodes,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$neighborDepth", normalized.NeighborDepth);
            command.Parameters.AddWithValue("$maxNodes", normalized.MaxNodes);
            command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
            command.ExecuteNonQuery();
        });
    }
}
