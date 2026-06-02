using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static partial class AllNotesFilterEngine
{
    internal static AllNotesFilterApplyResult Apply(
        IReadOnlyList<KnowledgeNoteListItemReadModel> allNotes,
        IReadOnlyList<AllNotesListItemViewModel> allNoteListItems,
        IReadOnlySet<string>? matchingRelativePaths,
        IReadOnlySet<string> selectedTagSet,
        IReadOnlySet<KnowledgeNoteListItemType> selectedTypeSet,
        AllNotesSortKey sortKey,
        bool hasActiveFilters)
    {
        List<int> matchingNoteIndexes = new(allNotes.Count);
        for (int allNotesIndex = 0; allNotesIndex < allNotes.Count; allNotesIndex++)
        {
            KnowledgeNoteListItemReadModel note = allNotes[allNotesIndex];
            if (matchingRelativePaths is not null && !matchingRelativePaths.Contains(note.RelativePath))
            {
                continue;
            }

            if (selectedTagSet.Count > 0 && !HasSelectedTag(note, selectedTagSet))
            {
                continue;
            }

            if (selectedTypeSet.Count > 0 && !selectedTypeSet.Contains(note.Type))
            {
                continue;
            }

            matchingNoteIndexes.Add(allNotesIndex);
        }

        SortMatchingNotes(matchingNoteIndexes, allNotes, sortKey);

        HashSet<string>? visibleRelativePaths = hasActiveFilters
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : null;
        AllNotesListItemViewModel[] filteredNotes = new AllNotesListItemViewModel[matchingNoteIndexes.Count];
        for (int index = 0; index < matchingNoteIndexes.Count; index++)
        {
            int allNotesIndex = matchingNoteIndexes[index];
            AllNotesListItemViewModel filteredNote = allNoteListItems[allNotesIndex];
            filteredNotes[index] = filteredNote;
            visibleRelativePaths?.Add(filteredNote.RelativePath);
        }

        return new AllNotesFilterApplyResult(filteredNotes, visibleRelativePaths);
    }

}

internal readonly record struct AllNotesFilterApplyResult(
    IReadOnlyList<AllNotesListItemViewModel> Notes,
    IReadOnlySet<string>? VisibleRelativePaths);
