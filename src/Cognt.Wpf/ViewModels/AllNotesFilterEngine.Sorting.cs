using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static partial class AllNotesFilterEngine
{
    private static void SortMatchingNotes(
        List<int> matchingNoteIndexes,
        IReadOnlyList<KnowledgeNoteListItemReadModel> notes,
        AllNotesSortKey sortKey)
    {
        matchingNoteIndexes.Sort((leftIndex, rightIndex) => CompareNotes(
            notes[leftIndex],
            leftIndex,
            notes[rightIndex],
            rightIndex,
            sortKey));
    }

    private static int CompareNotes(
        KnowledgeNoteListItemReadModel leftNote,
        int leftOriginalIndex,
        KnowledgeNoteListItemReadModel rightNote,
        int rightOriginalIndex,
        AllNotesSortKey sortKey)
    {
        int comparison = sortKey switch
        {
            AllNotesSortKey.CreatedOldest => leftNote.CreatedAt.CompareTo(rightNote.CreatedAt),
            AllNotesSortKey.CreatedNewest => rightNote.CreatedAt.CompareTo(leftNote.CreatedAt),
            AllNotesSortKey.ModifiedOldest => leftNote.UpdatedAt.CompareTo(rightNote.UpdatedAt),
            _ => rightNote.UpdatedAt.CompareTo(leftNote.UpdatedAt)
        };

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.OrdinalIgnoreCase.Compare(leftNote.Title, rightNote.Title);
        return comparison != 0
            ? comparison
            : leftOriginalIndex.CompareTo(rightOriginalIndex);
    }
}
