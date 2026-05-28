using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal static class DocumentMentionRows
{
    internal static DocumentMentionRow[] DistinctById(IReadOnlyList<DocumentMentionRow> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        List<DocumentMentionRow> distinctRows = new(rows.Count);
        foreach (DocumentMentionRow row in rows)
        {
            if (seenIds.Add(row.Id))
            {
                distinctRows.Add(row);
            }
        }

        return distinctRows.ToArray();
    }
}
