using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private const int RecentNotesLimit = 5;
    private const int RecentMentionRankLimit = 8;

    private void AddOrUpdateRecentNote(KnowledgeTreeItemReadModel? item, string? notePath)
    {
        if (item is null
            || item.Kind != KnowledgeItemKind.Note
            || string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        RemoveRecentNote(notePath);
        RecentNotes.Insert(0, new RecentNoteViewModel(item.Title, Path.GetFullPath(notePath)));

        while (RecentNotes.Count > RecentNotesLimit)
        {
            RecentNotes.RemoveAt(RecentNotes.Count - 1);
        }
    }

    private void UpdateRecentNoteTitle(string? notePath, string title)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = 0; index < RecentNotes.Count; index++)
        {
            RecentNoteViewModel recentNote = RecentNotes[index];
            if (string.Equals(
                    Path.GetFullPath(recentNote.NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes[index] = recentNote with { DisplayName = title };
                return;
            }
        }
    }

    private void RemoveRecentNote(string? notePath)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(notePath);

        for (int index = RecentNotes.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                    Path.GetFullPath(RecentNotes[index].NotePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                RecentNotes.RemoveAt(index);
            }
        }
    }

    private void PopulateRecentNotes(IEnumerable<KnowledgeNoteListItemReadModel> notes)
    {
        RecentNotes.Clear();

        List<KnowledgeNoteListItemReadModel> recentNotes = RecentNoteSelector.Select(notes, RecentNotesLimit);

        foreach (KnowledgeNoteListItemReadModel note in recentNotes)
        {
            RecentNotes.Add(new RecentNoteViewModel(
                note.Title,
                ResolveCollectionFilePath(openCollectionPath, note.RelativePath)));
        }
    }

    private IReadOnlyDictionary<string, int> CreateRecentMentionRanks()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        List<KnowledgeNoteListItemReadModel> recentNotes = RecentNoteSelector.Select(
            LoadNoteCatalog(openCollectionPath),
            RecentMentionRankLimit);

        var ranks = new Dictionary<string, int>(recentNotes.Count, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < recentNotes.Count; index++)
        {
            ranks.Add(recentNotes[index].RelativePath, index);
        }

        return ranks;
    }
}
