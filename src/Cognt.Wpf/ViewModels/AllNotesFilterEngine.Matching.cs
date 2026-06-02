using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static partial class AllNotesFilterEngine
{
    private static bool HasSelectedTag(
        KnowledgeNoteListItemReadModel note,
        IReadOnlySet<string> selectedTagSet)
    {
        foreach (string tag in note.Tags)
        {
            if (selectedTagSet.Contains(tag))
            {
                return true;
            }
        }

        return false;
    }
}
