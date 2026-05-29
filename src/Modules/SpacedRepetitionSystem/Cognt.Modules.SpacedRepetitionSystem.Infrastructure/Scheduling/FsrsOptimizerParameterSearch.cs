namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal static class FsrsOptimizerParameterSearch
{
    private const int NumberOfEpochs = 5;

    internal static double[] Compute(
        int filteredReviewCount,
        int minimumReviewLogCount,
        Func<IReadOnlyList<double>, double> computeLoss)
    {
        ArgumentNullException.ThrowIfNull(computeLoss);

        if (filteredReviewCount < minimumReviewLogCount)
        {
            return FsrsScheduler.DefaultParameters.ToArray();
        }

        double[] bestParameters = FsrsScheduler.DefaultParameters.ToArray();
        double bestLoss = computeLoss(bestParameters);
        double[] stepSizes = CreateInitialStepSizes();
        int[] parameterOrder = Enumerable.Range(0, bestParameters.Length).ToArray();
        Random random = new(42);

        for (int epoch = 0; epoch < NumberOfEpochs; epoch++)
        {
            Shuffle(parameterOrder, random);
            foreach (int index in parameterOrder)
            {
                double original = bestParameters[index];
                double bestValue = original;

                foreach (double direction in new[] { -1d, 1d })
                {
                    double candidateValue = ClampParameter(index, original + stepSizes[index] * direction);
                    if (candidateValue.Equals(original))
                    {
                        continue;
                    }

                    double[] candidate = bestParameters.ToArray();
                    candidate[index] = candidateValue;
                    double candidateLoss = computeLoss(candidate);
                    if (double.IsFinite(candidateLoss) && candidateLoss < bestLoss)
                    {
                        bestLoss = candidateLoss;
                        bestValue = candidateValue;
                    }
                }

                bestParameters[index] = bestValue;
            }

            for (int i = 0; i < stepSizes.Length; i++)
            {
                stepSizes[i] *= 0.55d;
            }
        }

        return bestParameters;
    }

    private static double[] CreateInitialStepSizes()
    {
        return Enumerable.Range(0, FsrsScheduler.LowerBoundsParameters.Length)
            .Select(static index => Math.Max(
                0.001d,
                (FsrsScheduler.UpperBoundsParameters[index] - FsrsScheduler.LowerBoundsParameters[index]) * 0.035d))
            .ToArray();
    }

    private static double ClampParameter(int index, double value)
        => Math.Clamp(
            value,
            FsrsScheduler.LowerBoundsParameters[index],
            FsrsScheduler.UpperBoundsParameters[index]);

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int index = random.Next(i + 1);
            (values[i], values[index]) = (values[index], values[i]);
        }
    }
}
