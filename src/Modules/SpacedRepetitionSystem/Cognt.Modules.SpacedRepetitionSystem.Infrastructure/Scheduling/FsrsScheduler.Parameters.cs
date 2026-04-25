namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    private static readonly double[] LowerBoundsParameters =
    [
        StabilityMin,
        StabilityMin,
        StabilityMin,
        StabilityMin,
        1.0,
        0.001,
        0.001,
        0.001,
        0.0,
        0.0,
        0.001,
        0.001,
        0.001,
        0.001,
        0.0,
        0.0,
        1.0,
        0.0,
        0.0,
        0.0,
        0.1
    ];

    private static readonly double[] UpperBoundsParameters =
    [
        InitialStabilityMax,
        InitialStabilityMax,
        InitialStabilityMax,
        InitialStabilityMax,
        10.0,
        4.0,
        4.0,
        0.75,
        4.5,
        0.8,
        3.5,
        5.0,
        0.25,
        0.9,
        4.0,
        1.0,
        6.0,
        2.0,
        2.0,
        0.8,
        0.8
    ];

    private static void ValidateParameters(IReadOnlyList<double> parameters)
    {
        if (parameters.Count != LowerBoundsParameters.Length)
        {
            throw new ArgumentException($"Expected {LowerBoundsParameters.Length} parameters, got {parameters.Count}.");
        }

        List<string> errors = [];
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i] < LowerBoundsParameters[i] || parameters[i] > UpperBoundsParameters[i])
            {
                errors.Add($"parameters[{i}] = {parameters[i]} is out of bounds: ({LowerBoundsParameters[i]}, {UpperBoundsParameters[i]})");
            }
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException("One or more parameters are out of bounds:\n" + string.Join('\n', errors));
        }
    }
}
