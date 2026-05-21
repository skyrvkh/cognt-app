using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed class FsrsReviewLog : IEquatable<FsrsReviewLog>
{
    internal FsrsReviewLog(long cardId, FsrsRating rating, DateTimeOffset reviewDatetime, int? reviewDuration = null)
    {
        CardId = cardId;
        Rating = rating;
        ReviewDatetime = reviewDatetime;
        ReviewDuration = reviewDuration;
    }

    internal long CardId { get; set; }

    internal FsrsRating Rating { get; set; }

    internal DateTimeOffset ReviewDatetime { get; set; }

    internal int? ReviewDuration { get; set; }

    internal FsrsReviewLogDictionary ToDictionary()
    {
        return new FsrsReviewLogDictionary
        {
            CardId = CardId,
            Rating = (int)Rating,
            ReviewDatetime = ReviewDatetime.ToString("O"),
            ReviewDuration = ReviewDuration
        };
    }

    internal static FsrsReviewLog FromDictionary(FsrsReviewLogDictionary source)
    {
        return new FsrsReviewLog(
            source.CardId,
            (FsrsRating)source.Rating,
            DateTimeOffset.Parse(source.ReviewDatetime),
            source.ReviewDuration);
    }

    internal string ToJson(bool writeIndented = false)
    {
        return JsonSerializer.Serialize(ToDictionary(), FsrsJson.Options(writeIndented));
    }

    internal static FsrsReviewLog FromJson(string sourceJson)
    {
        FsrsReviewLogDictionary source = JsonSerializer.Deserialize<FsrsReviewLogDictionary>(sourceJson, FsrsJson.Options())
            ?? throw new JsonException();
        return FromDictionary(source);
    }

    public bool Equals(FsrsReviewLog? other)
    {
        return other is not null
            && CardId == other.CardId
            && Rating == other.Rating
            && ReviewDatetime.Equals(other.ReviewDatetime)
            && ReviewDuration == other.ReviewDuration;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FsrsReviewLog);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CardId, Rating, ReviewDatetime, ReviewDuration);
    }
}

internal sealed class FsrsReviewLogDictionary
{
    [JsonPropertyName("card_id")]
    public long CardId { get; set; }

    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("review_datetime")]
    public string ReviewDatetime { get; set; } = string.Empty;

    [JsonPropertyName("review_duration")]
    public int? ReviewDuration { get; set; }
}
