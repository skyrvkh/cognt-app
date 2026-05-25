using System.IO;

namespace CogntApp.Shell.Collections;

internal sealed class FileSystemKnowledgeCollectionPathResolver : IKnowledgeCollectionPathResolver
{
    private const string CognCollectionDirectoryExtension = ".cognt";
    private const string DefaultCollectionName = "Default";

    public string CollectionDirectoryExtension => CognCollectionDirectoryExtension;

    public string ResolveDefaultCollectionPath()
    {
        foreach (string root in EnumerateCandidateRoots())
        {
            DirectoryInfo? directory = new(root);

            while (directory is not null)
            {
                string samplesCollectionPath = Path.Combine(
                    directory.FullName,
                    $"Samples{CognCollectionDirectoryExtension}");
                if (Directory.Exists(samplesCollectionPath))
                {
                    return samplesCollectionPath;
                }

                string? firstCollectionPath = FindFirstCollectionPath(directory.FullName);

                if (!string.IsNullOrWhiteSpace(firstCollectionPath))
                {
                    return firstCollectionPath;
                }

                directory = directory.Parent;
            }
        }

        return Path.Combine(Environment.CurrentDirectory, $"{DefaultCollectionName}{CognCollectionDirectoryExtension}");
    }

    public bool IsCollectionPath(string path)
    {
        string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

        return directoryName.EndsWith(CognCollectionDirectoryExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindFirstCollectionPath(string directoryPath)
    {
        try
        {
            string? firstCollectionPath = null;
            foreach (string path in Directory.EnumerateDirectories(
                         directoryPath,
                         $"*{CognCollectionDirectoryExtension}",
                         SearchOption.TopDirectoryOnly))
            {
                if (firstCollectionPath is null
                    || StringComparer.OrdinalIgnoreCase.Compare(path, firstCollectionPath) < 0)
                {
                    firstCollectionPath = path;
                }
            }

            return firstCollectionPath;
        }
        catch (Exception ex) when (IsRecoverableDirectoryDiscoveryException(ex))
        {
            return null;
        }
    }

    private static bool IsRecoverableDirectoryDiscoveryException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        HashSet<string> visitedRoots = new(StringComparer.OrdinalIgnoreCase);

        foreach (string root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            string fullPath = Path.GetFullPath(root);
            string normalizedPath = Path.TrimEndingDirectorySeparator(fullPath);

            if (visitedRoots.Add(normalizedPath))
            {
                yield return fullPath;
            }
        }
    }
}
