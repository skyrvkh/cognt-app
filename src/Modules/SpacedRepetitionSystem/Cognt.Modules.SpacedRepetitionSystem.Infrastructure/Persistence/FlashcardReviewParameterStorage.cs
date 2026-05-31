using System.Globalization;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal static partial class FlashcardReviewParameterStorage
{
    internal static string? Serialize(IReadOnlyList<double>? parameters)
    {
        double[] normalized = Normalize(parameters);
        return normalized.Length == 0
            ? null
            : string.Join(",", normalized.Select(parameter => parameter.ToString("G17", CultureInfo.InvariantCulture)));
    }

    internal static IReadOnlyList<double>? Deserialize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        string[] tokens = source.Split(
            [',', ';', ' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != FsrsScheduler.DefaultParameters.Length)
        {
            return null;
        }

        double[] parameters = new double[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || value < FsrsScheduler.LowerBoundsParameters[i]
                || value > FsrsScheduler.UpperBoundsParameters[i])
            {
                return null;
            }

            parameters[i] = value;
        }

        return parameters;
    }
}
