using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed class StoredFsrsCard
{
    internal StoredFsrsCard(
        FsrsCard Card,
        int Reps,
        int Lapses,
        int ElapsedDays,
        int ScheduledDays)
    {
        this.Card = Card;
        this.Reps = Reps;
        this.Lapses = Lapses;
        this.ElapsedDays = ElapsedDays;
        this.ScheduledDays = ScheduledDays;
    }

    internal FsrsCard Card { get; }

    internal int Reps { get; }

    internal int Lapses { get; }

    internal int ElapsedDays { get; }

    internal int ScheduledDays { get; }

    internal FsrsStoredCardDictionary ToSerializable(int version)
    {
        return new FsrsStoredCardDictionary
        {
            Version = version,
            Card = Card.ToDictionary(),
            Reps = Reps,
            Lapses = Lapses,
            ElapsedDays = ElapsedDays,
            ScheduledDays = ScheduledDays
        };
    }

    internal static StoredFsrsCard FromSerializable(FsrsStoredCardDictionary source)
    {
        return new StoredFsrsCard(
            FsrsCard.FromDictionary(source.Card ?? throw new JsonException("FSRS card state is missing.")),
            NormalizeCount(source.Reps),
            NormalizeCount(source.Lapses),
            NormalizeCount(source.ElapsedDays),
            NormalizeCount(source.ScheduledDays));
    }

    private static int NormalizeCount(int count)
    {
        return Math.Max(0, count);
    }
}

internal sealed class FsrsStoredCardDictionary
{
    [JsonInclude]
    public int Version { get; internal set; }

    [JsonInclude]
    public FsrsCardDictionary? Card { get; internal set; }

    [JsonInclude]
    public int Reps { get; internal set; }

    [JsonInclude]
    public int Lapses { get; internal set; }

    [JsonInclude]
    public int ElapsedDays { get; internal set; }

    [JsonInclude]
    public int ScheduledDays { get; internal set; }
}
