namespace OrleansPlayground;

public sealed class RemindersMaintenanceGrain(IGrainFactory grains)
    : Grain, IRemindersMaintenanceGrain
{
    public async Task<int> PurgeAsync(int count)
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("catalog").ListAsync();
        var toRemove = ids.Take(count).ToArray();
        int purged = 0;
        foreach (var id in toRemove)
            if (await grains.GetGrain<IReminderWorkerGrain>(id).UnregisterAsync())
                purged++;
        return purged;
    }
}
