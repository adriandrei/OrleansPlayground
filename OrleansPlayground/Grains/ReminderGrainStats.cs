namespace OrleansPlayground.Grains;

public interface IReminderStatsGrain : IGrainWithStringKey
{
    Task<ReminderClusterSnapshot> GetClusterStatsAsync();
}

[GenerateSerializer]
public record ReminderClusterSnapshot(
    int TotalTicks,
    double AverageDelayMs,
    Dictionary<string, int> PerSiloCounts,
    Dictionary<string, double> PerSiloAverageDelays,
    ReminderWorkerStats[] Workers);

public sealed class ReminderStatsGrain(IGrainFactory grains, ILogger<ReminderStatsGrain> logger)
    : Grain, IReminderStatsGrain
{
    public async Task<ReminderClusterSnapshot> GetClusterStatsAsync()
    {
        var catalog = grains.GetGrain<IWorkerCatalogGrain>("catalog");
        var ids = await catalog.ListAsync();

        var stats = await Task.WhenAll(ids.Select(id =>
            grains.GetGrain<IReminderWorkerGrainWithState>(id).GetStatsAsync()));

        var totalTicks = stats.Sum(s => s.TotalTicks);
        var avgDelay = stats.Length > 0 ? stats.Average(s => s.AverageDelayMs) : 0;

        var perSiloCounts = new Dictionary<string, int>();
        var perSiloAvgDelay = new Dictionary<string, double>();

        foreach (var worker in stats)
        {
            foreach (var kv in worker.PerSiloCounts)
                perSiloCounts[kv.Key] = perSiloCounts.GetValueOrDefault(kv.Key) + kv.Value;

            foreach (var kv in worker.PerSiloAverageDelays)
                perSiloAvgDelay[kv.Key] =
                    perSiloAvgDelay.TryGetValue(kv.Key, out var current)
                        ? (current + kv.Value) / 2
                        : kv.Value;
        }

        return new ReminderClusterSnapshot(totalTicks, avgDelay, perSiloCounts, perSiloAvgDelay, stats);
    }
}