namespace CogntApp.ViewModels;

internal static class CountDisplayFormatter
{
    public static string Format(int count, string noun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noun);

        int normalizedCount = Math.Max(0, count);

        return normalizedCount == 1
            ? $"1 {noun}"
            : $"{normalizedCount:N0} {noun}s";
    }
}
