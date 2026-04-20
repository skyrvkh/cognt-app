using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static partial class FsrsStoredStateCodec
{
    private static bool TryReadStoredCardDictionary(
        string serializedState,
        [NotNullWhen(true)] out StoredFsrsCard? storedCard)
    {
        try
        {
            FsrsStoredCardDictionary? dictionary = JsonSerializer.Deserialize<FsrsStoredCardDictionary>(
                serializedState,
                JsonOptions);

            if (dictionary?.Card is null)
            {
                storedCard = null;
                return false;
            }

            storedCard = StoredFsrsCard.FromSerializable(dictionary);
            return true;
        }
        catch (Exception ex) when (IsRecoverableStateParseException(ex))
        {
            storedCard = null;
            return false;
        }
    }

    private static bool TryReadFsrsCard(
        string serializedState,
        [NotNullWhen(true)] out StoredFsrsCard? storedCard)
    {
        try
        {
            storedCard = CreateStoredCardFromFsrsCard(FsrsCard.FromJson(serializedState));
            return true;
        }
        catch (Exception ex) when (IsRecoverableStateParseException(ex))
        {
            storedCard = null;
            return false;
        }
    }

    private static StoredFsrsCard ReadLegacyDeterministicState(string serializedState)
    {
        try
        {
            DateTimeOffset backfilledAt = DateTimeOffset.UtcNow;
            using JsonDocument document = JsonDocument.Parse(serializedState);
            JsonElement root = document.RootElement;
            int reviewCount = GetInt(root, LegacyReviewCountPropertyName) ?? 0;
            int lapseCount = GetInt(root, LegacyLapseCountPropertyName) ?? 0;
            double? stability = GetDouble(root, LegacyStabilityPropertyName);
            double? difficulty = NormalizeDifficulty(GetDouble(root, LegacyDifficultyPropertyName));
            DateTimeOffset dueUtc = GetDateTimeOffset(root, LegacyDueUtcPropertyName) ?? backfilledAt;
            FlashcardLearningState legacyState = GetLearningState(root, LegacyStatePropertyName) ?? FlashcardLearningState.New;

            return CreateBackfilledCard(dueUtc, legacyState, stability, difficulty, reviewCount, lapseCount, backfilledAt);
        }
        catch (JsonException)
        {
            return CreateDefaultBackfilledCard();
        }
    }

}
