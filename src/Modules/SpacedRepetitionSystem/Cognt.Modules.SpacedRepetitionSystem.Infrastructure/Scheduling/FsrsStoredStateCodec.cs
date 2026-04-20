using System.Text.Json;
using System.Text.Json.Serialization;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static partial class FsrsStoredStateCodec
{
    private const int CurrentStoredStateVersion = 1;
    private const string LegacyReviewCountPropertyName = "reviewCount";
    private const string LegacyLapseCountPropertyName = "lapseCount";
    private const string LegacyStabilityPropertyName = "stability";
    private const string LegacyDifficultyPropertyName = "difficulty";
    private const string LegacyDueUtcPropertyName = "dueUtc";
    private const string LegacyStatePropertyName = "state";
    private const double StabilityMin = 0.001;
    private const double MinDifficulty = 1.0;
    private const double MaxDifficulty = 10.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string Serialize(StoredFsrsCard stored)
    {
        return JsonSerializer.Serialize(stored.ToSerializable(CurrentStoredStateVersion), JsonOptions);
    }

    internal static StoredFsrsCard Read(string serializedState)
    {
        if (string.IsNullOrWhiteSpace(serializedState))
        {
            return CreateDefaultBackfilledCard();
        }

        if (TryReadStoredCardDictionary(serializedState, out StoredFsrsCard? storedCard))
        {
            return storedCard;
        }

        if (TryReadFsrsCard(serializedState, out StoredFsrsCard? fsrsCard))
        {
            return fsrsCard;
        }

        return ReadLegacyDeterministicState(serializedState);
    }

}
