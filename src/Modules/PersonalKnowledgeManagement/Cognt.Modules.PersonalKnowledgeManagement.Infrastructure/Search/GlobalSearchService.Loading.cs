using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
    private IReadOnlyList<SearchCandidate> LoadSearchCandidates(
        GlobalSearchCollectionScope collection,
        string query)
    {
        try
        {
            IReadOnlySet<string> matchedRelativePaths = noteService.SearchNoteCatalog(collection.CollectionId, query);
            if (matchedRelativePaths.Count == 0)
            {
                return [];
            }

            return noteService.LoadNoteCatalog(collection.CollectionId)
                .Where(note => matchedRelativePaths.Contains(note.RelativePath))
                .Select(note => new SearchCandidate(note))
                .ToArray();
        }
        catch (Exception ex) when (IsRecoverableSearchException(ex))
        {
            return [];
        }
    }

    private string? ReadNoteContent(string collectionId, string relativePath)
    {
        try
        {
            return noteService.ReadNoteFile(collectionId, relativePath);
        }
        catch (Exception ex) when (IsRecoverableSearchException(ex))
        {
            return null;
        }
    }

    private static bool IsRecoverableSearchException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or NotSupportedException
            or ArgumentException;
    }

    private sealed class SearchCandidate
    {
        internal SearchCandidate(KnowledgeNoteListItemReadModel note)
        {
            Note = note;
        }

        internal KnowledgeNoteListItemReadModel Note { get; }
    }
}
