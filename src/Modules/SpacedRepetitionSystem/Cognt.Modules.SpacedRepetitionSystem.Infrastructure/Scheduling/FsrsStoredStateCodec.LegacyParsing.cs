using System.Text.Json;
using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static partial class FsrsStoredStateCodec
{
    private static int? GetInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static double? GetDouble(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset result)
            ? result.ToUniversalTime()
            : null;
    }

    private static FlashcardLearningState? GetLearningState(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return (FlashcardLearningState)value.GetInt32();
        }

        return value.ValueKind == JsonValueKind.String
            && Enum.TryParse(value.GetString(), ignoreCase: true, out FlashcardLearningState state)
                ? state
                : null;
    }

    private static double? NormalizeDifficulty(double? difficulty)
    {
        if (!difficulty.HasValue)
        {
            return null;
        }

        return difficulty.Value <= 1.0
            ? ClampDifficulty(difficulty.Value * MaxDifficulty)
            : ClampDifficulty(difficulty.Value);
    }

    private static double ClampDifficulty(double difficulty)
    {
        return Math.Clamp(difficulty, MinDifficulty, MaxDifficulty);
    }

    private static int NormalizeCount(int count)
    {
        return Math.Max(0, count);
    }

    private static bool IsRecoverableStateParseException(Exception exception)
    {
        return exception is JsonException or FormatException;
    }
}
