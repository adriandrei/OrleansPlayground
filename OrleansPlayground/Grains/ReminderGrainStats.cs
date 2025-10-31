namespace OrleansPlayground.Grains;

public interface IReminderStatsGrain : IGrainWithStringKey
{
    Task<ReminderClusterSnapshot> GetClusterStatsAsync();
}

[GenerateSerializer]
public record ReminderClusterSnapshot(
    int TotalTicks,
    double AverageTicksPerGrain,
    Dictionary<string, int> PerSiloCounts,
    ReminderWorkerStats[] Workers);

public sealed class ReminderStatsGrain(IGrainFactory grains, ILogger<ReminderStatsGrain> logger)
    : Grain, IReminderStatsGrain
{
    public async Task<ReminderClusterSnapshot> GetClusterStatsAsync()
    {
        var catalog = grains.GetGrain<IWorkerCatalogGrain>("catalog");
        var ids = await catalog.ListAsync();

        var statsTasks = ids.Select(id =>
            grains.GetGrain<IReminderWorkerGrain>(id).GetStatsAsync()).ToArray();

        var results = await Task.WhenAll(statsTasks);

        var total = results.Sum(r => r.TickCount);
        var avg = results.Length > 0 ? total / (double)results.Length : 0;
        var perSilo = results.GroupBy(r => r.LastSilo)
                             .ToDictionary(g => g.Key, g => g.Sum(r => r.TickCount));

        return new ReminderClusterSnapshot(total, avg, perSilo, results);
    }
}

