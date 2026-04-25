namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    internal const double FsrsDefaultDecay = 0.1542;
    internal const double StabilityMin = 0.001;
    internal const double InitialStabilityMax = 100.0;
    internal const double MinDifficulty = 1.0;
    internal const double MaxDifficulty = 10.0;

    internal static readonly double[] DefaultParameters =
    [
        0.212,
        1.2931,
        2.3065,
        8.2956,
        6.4133,
        0.8334,
        3.0194,
        0.001,
        1.8722,
        0.1666,
        0.796,
        1.4835,
        0.0614,
        0.2629,
        1.6483,
        0.6014,
        1.8729,
        0.5425,
        0.0912,
        0.0658,
        FsrsDefaultDecay
    ];

    private static readonly FuzzRange[] FuzzRanges =
    [
        new(2.5, 7.0, 0.15),
        new(7.0, 20.0, 0.1),
        new(20.0, double.PositiveInfinity, 0.05)
    ];
}
