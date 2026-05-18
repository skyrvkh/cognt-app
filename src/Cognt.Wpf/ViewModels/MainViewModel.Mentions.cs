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
            return noteService.ListMentionTargets(openCollectionPath)
                .OrderBy(target => recentRanks.TryGetValue(target.RelativePath, out int rank) ? rank : int.MaxValue)
                .ThenBy(target => target.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(target => target.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
}
