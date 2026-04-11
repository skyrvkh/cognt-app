using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsCard : IEquatable<FsrsCard>
{
    internal FsrsCard(
        long? cardId = null,
        FsrsState state = FsrsState.Learning,
        int? step = null,
        double? stability = null,
        double? difficulty = null,
        DateTimeOffset? due = null,
        DateTimeOffset? lastReview = null)
    {
        if (cardId is null)
        {
            CardId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Thread.Sleep(1);
        }
        else
        {
            CardId = cardId.Value;
        }

        State = state;
        Step = State == FsrsState.Learning && step is null ? 0 : step;
        Stability = stability;
        Difficulty = difficulty;
        Due = due ?? DateTimeOffset.UtcNow;
        LastReview = lastReview;
    }

    internal long CardId { get; set; }

    internal FsrsState State { get; set; }

    internal int? Step { get; set; }

    internal double? Stability { get; set; }

    internal double? Difficulty { get; set; }

    internal DateTimeOffset Due { get; set; }

    internal DateTimeOffset? LastReview { get; set; }

    internal FsrsCard Copy()
    {
        return new FsrsCard(CardId, State, Step, Stability, Difficulty, Due, LastReview);
    }

    public bool Equals(FsrsCard? other)
    {
        return other is not null
            && CardId == other.CardId
            && State == other.State
            && Step == other.Step
            && Nullable.Equals(Stability, other.Stability)
            && Nullable.Equals(Difficulty, other.Difficulty)
            && Due.Equals(other.Due)
            && Nullable.Equals(LastReview, other.LastReview);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FsrsCard);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CardId, State, Step, Stability, Difficulty, Due, LastReview);
    }
}
