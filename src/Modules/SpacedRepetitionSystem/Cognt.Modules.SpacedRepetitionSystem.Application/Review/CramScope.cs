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
            DeckIds = DeckIds
                .Where(id => id > 0)
                .ToHashSet(),
            DocumentIds = NormalizeStrings(DocumentIds),
            BlockIds = NormalizeStrings(BlockIds),
            TagIds = NormalizeStrings(TagIds),
        };
    }

    private static IReadOnlySet<string> NormalizeStrings(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
