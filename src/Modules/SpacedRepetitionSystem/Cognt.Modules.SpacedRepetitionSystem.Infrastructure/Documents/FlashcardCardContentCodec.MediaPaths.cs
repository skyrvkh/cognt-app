namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static string? ResolveCollectionMediaPath(string? collectionRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(collectionRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            string fullRoot = Path.GetFullPath(collectionRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(
                fullRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            string normalizedRoot = fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
        catch
        {
            return null;
        }
    }
}
