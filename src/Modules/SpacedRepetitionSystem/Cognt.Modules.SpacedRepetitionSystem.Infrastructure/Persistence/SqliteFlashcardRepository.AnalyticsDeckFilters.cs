using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static string AddDeckNameFilter(
        SqliteCommand command,
        IReadOnlyList<string> deckNames,
        string deckIdExpression)
    {
        string[] normalizedDeckNames = deckNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedDeckNames.Length == 0)
        {
            return string.Empty;
        }

        string[] parameterNames = new string[normalizedDeckNames.Length];
        for (int index = 0; index < normalizedDeckNames.Length; index++)
        {
            string parameterName = string.Concat("$deckName", command.Parameters.Count);
            parameterNames[index] = parameterName;
            command.Parameters.AddWithValue(parameterName, normalizedDeckNames[index]);
        }

        return string.Concat(
            "AND ",
            deckIdExpression,
            " IN (SELECT id FROM flashcard_deck WHERE name IN (",
            string.Join(", ", parameterNames),
            "))");
    }
}
