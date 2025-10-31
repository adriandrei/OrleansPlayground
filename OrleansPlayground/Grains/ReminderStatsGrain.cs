using Orleans;
using System.Collections.Concurrent;

namespace OrleansPlayground.Grains;

public interface IReminderStatsGrain : IGrainWithStringKey
{
    Task RecordAsync(string grainId, string silo, DateTime expected, DateTime actual, double delayMs, double durationMs);
    Task<ReminderStatsSnapshot> GetSnapshotAsync();
}


[GenerateSerializer]
public record ReminderTickSample(
    string GrainId,
    string Silo,
    DateTime Expected,
    DateTime Actual,
    double DelayMs,
    double DurationMs);

[GenerateSerializer]
public record ReminderStatsSnapshot(
    int TotalTicks,
    double AverageDelayMs,
    double MaxDelayMs,
    double AverageDurationMs,
    Dictionary<string, int> PerSiloCounts,
    Dictionary<string, int> PerGrainTickCounts);

public sealed class ReminderStatsGrain(ILogger<ReminderStatsGrain> logger)
    : Grain, IReminderStatsGrain
{
    private readonly ConcurrentBag<ReminderTickSample> _samples = new();
    private const int MaxSamples = 5000;

    public Task RecordAsync(string grainId, string silo, DateTime expected, DateTime actual, double delayMs, double durationMs)
    {
        _samples.Add(new ReminderTickSample(grainId, silo, expected, actual, delayMs, durationMs));

        // cap memory
        if (_samples.Count > MaxSamples)
        {
            while (_samples.TryTake(out _)) { }
        }

        return Task.CompletedTask;
    }

    public Task<ReminderStatsSnapshot> GetSnapshotAsync()
    {
        var samples = _samples.ToArray();
        if (samples.Length == 0)
            return Task.FromResult(new ReminderStatsSnapshot(0, 0, 0, 0, new(), new()));

        var perSilo = samples.GroupBy(s => s.Silo)
                             .ToDictionary(g => g.Key, g => g.Count());

        var perGrain = samples.GroupBy(s => s.GrainId)
                              .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(new ReminderStatsSnapshot(
            TotalTicks: samples.Length,
            AverageDelayMs: samples.Average(s => s.DelayMs),
            MaxDelayMs: samples.Max(s => s.DelayMs),
            AverageDurationMs: samples.Average(s => s.DurationMs),
            PerSiloCounts: perSilo,
            PerGrainTickCounts: perGrain
        ));
    }
}