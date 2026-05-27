namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed record CramScope
{
    public static CramScope All { get; } = new();

    public IReadOnlySet<long> DeckIds { get; init; } = new HashSet<long>();

    public bool IncludeSubdecks { get; init; } = true;

    public bool UsesAllDecks => DeckIds.Count == 0;

    public IReadOnlySet<string> DocumentIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> BlockIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> TagIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool ExcludeSuspendedCards { get; init; } = true;

    public CramScope Normalize()
    {
        return this with
        {
            DeckIds = NormalizeDeckIds(DeckIds),
            DocumentIds = NormalizeStrings(DocumentIds),
            BlockIds = NormalizeStrings(BlockIds),
            TagIds = NormalizeStrings(TagIds),
        };
    }

    private static IReadOnlySet<long> NormalizeDeckIds(IEnumerable<long> values)
    {
        HashSet<long> normalized = [];
        foreach (long value in values)
        {
            if (value > 0)
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static IReadOnlySet<string> NormalizeStrings(IEnumerable<string> values)
    {
        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized.Add(value.Trim());
        }

        return normalized;
    }
}
