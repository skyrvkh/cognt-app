namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    private TimeSpan GetFuzzedInterval(TimeSpan interval)
    {
        double intervalDays = interval.Days;
        if (intervalDays < 2.5)
        {
            return interval;
        }

        double delta = 1.0;
        foreach (FuzzRange fuzzRange in FuzzRanges)
        {
            delta += fuzzRange.Factor * Math.Max(Math.Min(intervalDays, fuzzRange.End) - fuzzRange.Start, 0.0);
        }

        int minInterval = (int)Math.Round(intervalDays - delta);
        int maxInterval = (int)Math.Round(intervalDays + delta);
        minInterval = Math.Max(2, minInterval);
        maxInterval = Math.Min(maxInterval, MaximumInterval);
        minInterval = Math.Min(minInterval, maxInterval);

        double fuzzedIntervalDays = random.NextDouble() * (maxInterval - minInterval + 1) + minInterval;
        int rounded = Math.Min((int)Math.Round(fuzzedIntervalDays), MaximumInterval);
        return TimeSpan.FromDays(rounded);
    }

    public bool Equals(FsrsScheduler? other)
    {
        return other is not null
            && Parameters.SequenceEqual(other.Parameters)
            && DesiredRetention.Equals(other.DesiredRetention)
            && LearningSteps.SequenceEqual(other.LearningSteps)
            && RelearningSteps.SequenceEqual(other.RelearningSteps)
            && MaximumInterval == other.MaximumInterval
            && EnableFuzzing == other.EnableFuzzing;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FsrsScheduler);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (double parameter in Parameters)
        {
            hash.Add(parameter);
        }

        hash.Add(DesiredRetention);
        foreach (TimeSpan step in LearningSteps)
        {
            hash.Add(step);
        }

        foreach (TimeSpan step in RelearningSteps)
        {
            hash.Add(step);
        }

        hash.Add(MaximumInterval);
        hash.Add(EnableFuzzing);
        return hash.ToHashCode();
    }

    private readonly record struct FuzzRange(double Start, double End, double Factor);
}
