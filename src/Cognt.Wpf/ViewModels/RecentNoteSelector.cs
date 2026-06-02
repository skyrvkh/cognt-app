using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static class RecentNoteSelector
{
    internal static List<KnowledgeNoteListItemReadModel> Select(
        IEnumerable<KnowledgeNoteListItemReadModel> notes,
        int limit)
    {
        var recentNotes = new List<KnowledgeNoteListItemReadModel>(limit);

        foreach (KnowledgeNoteListItemReadModel note in notes)
        {
            if (string.IsNullOrWhiteSpace(note.RelativePath))
            {
                continue;
            }

            int insertIndex = 0;
            while (insertIndex < recentNotes.Count
                   && CompareRecentNoteOrder(recentNotes[insertIndex], note) <= 0)
            {
                insertIndex++;
            }

            if (insertIndex >= limit)
            {
                continue;
            }

            recentNotes.Insert(insertIndex, note);

            if (recentNotes.Count > limit)
            {
                recentNotes.RemoveAt(recentNotes.Count - 1);
            }
        }

        return recentNotes;
    }

    private static int CompareRecentNoteOrder(
        KnowledgeNoteListItemReadModel left,
        KnowledgeNoteListItemReadModel right)
    {
        int updatedAtComparison = right.UpdatedAt.CompareTo(left.UpdatedAt);
        if (updatedAtComparison != 0)
        {
            return updatedAtComparison;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(
            Path.GetFileName(left.RelativePath),
            Path.GetFileName(right.RelativePath));
    }
}
