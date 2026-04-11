using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsCard
{
    internal FsrsCardDictionary ToDictionary()
    {
        return new FsrsCardDictionary
        {
            CardId = CardId,
            State = (int)State,
            Step = Step,
            Stability = Stability,
            Difficulty = Difficulty,
            Due = Due.ToString("O"),
            LastReview = LastReview?.ToString("O")
        };
    }

    internal static FsrsCard FromDictionary(FsrsCardDictionary source)
    {
        return new FsrsCard(
            source.CardId,
            (FsrsState)source.State,
            source.Step,
            source.Stability,
            source.Difficulty,
            DateTimeOffset.Parse(source.Due),
            source.LastReview is null ? null : DateTimeOffset.Parse(source.LastReview));
    }

    internal string ToJson(bool writeIndented = false)
    {
        return JsonSerializer.Serialize(ToDictionary(), FsrsJson.Options(writeIndented));
    }

    internal static FsrsCard FromJson(string sourceJson)
    {
        FsrsCardDictionary source = JsonSerializer.Deserialize<FsrsCardDictionary>(sourceJson, FsrsJson.Options())
            ?? throw new JsonException();
        return FromDictionary(source);
    }
}

internal sealed class FsrsCardDictionary
{
    [JsonPropertyName("card_id")]
    public long CardId { get; set; }

    [JsonPropertyName("state")]
    public int State { get; set; }

    [JsonPropertyName("step")]
    public int? Step { get; set; }

    [JsonPropertyName("stability")]
    public double? Stability { get; set; }

    [JsonPropertyName("difficulty")]
    public double? Difficulty { get; set; }

    [JsonPropertyName("due")]
    public string Due { get; set; } = string.Empty;

    [JsonPropertyName("last_review")]
    public string? LastReview { get; set; }
}
