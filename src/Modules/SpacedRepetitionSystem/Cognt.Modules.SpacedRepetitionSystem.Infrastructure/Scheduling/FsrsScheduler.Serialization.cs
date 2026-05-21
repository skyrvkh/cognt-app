using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    internal FsrsSchedulerDictionary ToDictionary()
    {
        return new FsrsSchedulerDictionary
        {
            Parameters = Parameters.ToArray(),
            DesiredRetention = DesiredRetention,
            LearningSteps = LearningSteps
                .Select(step => (int)Math.Round(step.TotalSeconds))
                .ToArray(),
            RelearningSteps = RelearningSteps
                .Select(step => (int)Math.Round(step.TotalSeconds))
                .ToArray(),
            MaximumInterval = MaximumInterval,
            EnableFuzzing = EnableFuzzing,
        };
    }

    internal static FsrsScheduler FromDictionary(FsrsSchedulerDictionary source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new FsrsScheduler(
            source.Parameters,
            source.DesiredRetention,
            source.LearningSteps.Select(step => TimeSpan.FromSeconds(step)).ToArray(),
            source.RelearningSteps.Select(step => TimeSpan.FromSeconds(step)).ToArray(),
            source.MaximumInterval,
            source.EnableFuzzing);
    }

    internal string ToJson(bool writeIndented = false)
    {
        return JsonSerializer.Serialize(ToDictionary(), FsrsJson.Options(writeIndented));
    }

    internal static FsrsScheduler FromJson(string sourceJson)
    {
        FsrsSchedulerDictionary source = JsonSerializer.Deserialize<FsrsSchedulerDictionary>(sourceJson, FsrsJson.Options())
            ?? throw new JsonException();
        return FromDictionary(source);
    }
}

internal sealed class FsrsSchedulerDictionary
{
    [JsonPropertyName("parameters")]
    public double[] Parameters { get; set; } = [];

    [JsonPropertyName("desired_retention")]
    public double DesiredRetention { get; set; }

    [JsonPropertyName("learning_steps")]
    public int[] LearningSteps { get; set; } = [];

    [JsonPropertyName("relearning_steps")]
    public int[] RelearningSteps { get; set; } = [];

    [JsonPropertyName("maximum_interval")]
    public int MaximumInterval { get; set; }

    [JsonPropertyName("enable_fuzzing")]
    public bool EnableFuzzing { get; set; }
}
