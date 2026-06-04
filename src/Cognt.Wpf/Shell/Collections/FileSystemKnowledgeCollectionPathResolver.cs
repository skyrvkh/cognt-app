using System.IO;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Collections;

internal sealed class FileSystemKnowledgeCollectionPathResolver : IKnowledgeCollectionPathResolver
{
    private const string CognCollectionDirectoryExtension = ".cognt";
    private const string DefaultCollectionName = "Default";
    private readonly ILogger<FileSystemKnowledgeCollectionPathResolver> logger;

    public FileSystemKnowledgeCollectionPathResolver(ILogger<FileSystemKnowledgeCollectionPathResolver> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

        string fallbackPath = Path.Combine(Environment.CurrentDirectory, $"{DefaultCollectionName}{CognCollectionDirectoryExtension}");
        return fallbackPath;
    }

    public bool IsCollectionPath(string path)
    {
        string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

        return directoryName.EndsWith(CognCollectionDirectoryExtension, StringComparison.OrdinalIgnoreCase);
    }

    private string? FindFirstCollectionPath(string directoryPath)
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
            logger.LogWarning(ex, "First collection path discovery recovered from directory read failure. DirectoryPath={DirectoryPath}", directoryPath);
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
