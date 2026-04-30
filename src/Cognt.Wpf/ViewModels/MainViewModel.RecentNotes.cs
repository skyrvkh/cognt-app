using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
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

        while (RecentNotes.Count > 5)
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

        foreach (KnowledgeNoteListItemReadModel note in notes
                     .OrderByDescending(item => item.UpdatedAt)
                     .ThenBy(item => Path.GetFileName(item.RelativePath), StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(note.RelativePath))
            {
                continue;
            }

            RecentNotes.Add(new RecentNoteViewModel(
                note.Title,
                ResolveCollectionFilePath(openCollectionPath, note.RelativePath)));

            if (RecentNotes.Count >= 5)
            {
                break;
            }
        }
    }

    private IReadOnlyDictionary<string, int> CreateRecentMentionRanks()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return LoadNoteCatalog(openCollectionPath)
            .Where(note => !string.IsNullOrWhiteSpace(note.RelativePath))
            .OrderByDescending(note => note.UpdatedAt)
            .ThenBy(note => Path.GetFileName(note.RelativePath), StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select((note, index) => new
            {
                RelativePath = note.RelativePath,
                Rank = index,
            })
            .ToDictionary(item => item.RelativePath, item => item.Rank, StringComparer.OrdinalIgnoreCase);
    }
}
