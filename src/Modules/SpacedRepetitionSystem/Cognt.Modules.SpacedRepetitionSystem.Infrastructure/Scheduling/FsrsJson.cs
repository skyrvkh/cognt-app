using System.Text.Json;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static class FsrsJson
{
    internal static JsonSerializerOptions Options(bool writeIndented = false)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true
        };
    }
}
