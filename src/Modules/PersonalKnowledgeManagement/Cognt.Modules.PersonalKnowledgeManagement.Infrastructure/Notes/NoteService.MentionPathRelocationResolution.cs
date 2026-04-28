using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private string? TryResolveRelocatedMentionPath(string root, string relativePath)
    {
        string? normalizedTarget = NormalizeDocumentTarget(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedTarget) || !IsNoteRelativePath(normalizedTarget))
        {
            return null;
        }

        string targetRelativePath = NormalizeRelativePath(normalizedTarget).TrimStart('/');
        string targetFileName = Path.GetFileName(ToPlatformPath(targetRelativePath));
        string targetStemKey = NormalizeDocumentTitleLookupKey(Path.GetFileNameWithoutExtension(targetFileName));
        if (string.IsNullOrWhiteSpace(targetFileName) || string.IsNullOrWhiteSpace(targetStemKey))
        {
            return null;
        }

        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        return LoadRelocatedMentionPathCandidates(
                root,
                repository,
                targetRelativePath,
                targetFileName,
                targetStemKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray() is [string single]
                ? single
                : null;
    }

    private static IEnumerable<string> LoadRelocatedMentionPathCandidates(
        string root,
        ITreeRepository repository,
        string targetRelativePath,
        string targetFileName,
        string targetStemKey)
    {
        foreach (KnowledgeTreeItemRow item in repository.LoadNoteItems())
        {
            if (TryGetExistingNoteItemFullPath(root, item) is not { } itemFullPath)
            {
                continue;
            }

            string itemRelativePath = NormalizeRelativePath(item.FileRelPath!);
            if (MatchesRelocatedMentionTarget(
                item,
                itemRelativePath,
                itemFullPath,
                targetRelativePath,
                targetFileName,
                targetStemKey))
            {
                yield return itemFullPath;
            }
        }
    }

    private static string? TryGetExistingNoteItemFullPath(string root, KnowledgeTreeItemRow item)
    {
        if (string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            return null;
        }

        string itemRelativePath = NormalizeRelativePath(item.FileRelPath);
        string itemFullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(itemRelativePath)));
        return IsPathInsideRoot(itemFullPath, root) && File.Exists(itemFullPath)
            ? itemFullPath
            : null;
    }

    private static bool MatchesRelocatedMentionTarget(
        KnowledgeTreeItemRow item,
        string itemRelativePath,
        string itemFullPath,
        string targetRelativePath,
        string targetFileName,
        string targetStemKey)
    {
        string itemFileName = Path.GetFileName(itemFullPath);
        string itemStemKey = NormalizeDocumentTitleLookupKey(Path.GetFileNameWithoutExtension(itemFileName));
        string itemTitleKey = NormalizeDocumentTitleLookupKey(item.Title);

        return string.Equals(itemRelativePath, targetRelativePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemFileName, targetFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemStemKey, targetStemKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemTitleKey, targetStemKey, StringComparison.OrdinalIgnoreCase);
    }
}
