namespace OrleansPlayground.Grains;

public interface IRemindersMaintenanceGrain : IGrainWithStringKey
{
    Task<int> Purge<T>(int count, string grainPrimaryKey) where T : IMyGrain;
}

public sealed class RemindersMaintenanceGrain(IGrainFactory grains)
    : Grain, IRemindersMaintenanceGrain
{
    public async Task<int> Purge<T>(int count, string grainPrimaryKey) where T : IMyGrain
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>(grainPrimaryKey).ListAsync();
        var toRemove = ids.Take(count).ToArray();
        int purged = 0;
        foreach (var id in toRemove)
        {
            await grains.GetGrain<T>(id).UnregisterReminderAsync();
            purged++;
        }

        return purged;
    }
}
