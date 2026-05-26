using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public string ReadMentionPreview(string relativePath, int maxCharacters)
    {
        return noteService.ReadMentionPreview(openCollectionPath, relativePath, maxCharacters);
    }

    public KnowledgeDocumentLinksReadModel ReadCurrentDocumentLinks(string currentContent)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || string.IsNullOrWhiteSpace(currentNotePath))
        {
            return new KnowledgeDocumentLinksReadModel([], []);
        }

        return noteService.ReadDocumentLinks(
            openCollectionPath,
            GetRequiredCurrentNoteRelativePath(),
            currentContent);
    }

    internal KnowledgeDocumentLinksReadModel ReadDocumentLinks(
        string collectionPath,
        string relativePath,
        string currentContent)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return new KnowledgeDocumentLinksReadModel([], []);
        }

        return noteService.ReadDocumentLinks(
            collectionPath,
            relativePath,
            currentContent);
    }

    public void SaveMentionSource(string sourcePath, string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The mention source is not the current note file.");
        }

        SaveNoteIfChanged(content);
    }

    public IReadOnlyList<KnowledgeMentionTargetReadModel> ListMentionTargets()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
        {
            return [];
        }

        try
        {
            IReadOnlyDictionary<string, int> recentRanks = CreateRecentMentionRanks();
            List<KnowledgeMentionTargetReadModel> targets = [.. noteService.ListMentionTargets(openCollectionPath)];
            targets.Sort((left, right) => CompareMentionTargets(left, right, recentRanks));
            return targets;
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            RaiseMessage("Mention Index Failed", $"Mentions could not be indexed: {ex.Message}");
            return [];
        }
    }

    public IReadOnlyList<KnowledgeMentionTargetReadModel> SearchMentionTargets(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
        {
            return [];
        }

        try
        {
            return noteService.SearchMentionTargets(openCollectionPath, query, limit);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            RaiseMessage("Mention Search Failed", $"Mentions could not be searched: {ex.Message}");
            return [];
        }
    }

    private static int CompareMentionTargets(
        KnowledgeMentionTargetReadModel left,
        KnowledgeMentionTargetReadModel right,
        IReadOnlyDictionary<string, int> recentRanks)
    {
        int leftRank = recentRanks.TryGetValue(left.RelativePath, out int leftRecentRank)
            ? leftRecentRank
            : int.MaxValue;
        int rightRank = recentRanks.TryGetValue(right.RelativePath, out int rightRecentRank)
            ? rightRecentRank
            : int.MaxValue;
        int rankComparison = leftRank.CompareTo(rightRank);
        if (rankComparison != 0)
        {
            return rankComparison;
        }

        int titleComparison = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        return titleComparison != 0
            ? titleComparison
            : string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
    }
}
