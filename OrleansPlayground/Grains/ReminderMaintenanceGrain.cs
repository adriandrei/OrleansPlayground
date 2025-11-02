namespace OrleansPlayground.Grains;

public interface IRemindersMaintenanceGrain : IGrainWithStringKey
{
    Task<int> Purge(int count, string grainPrimaryKey);
}

public sealed class RemindersMaintenanceGrain(IGrainFactory grains)
    : Grain, IRemindersMaintenanceGrain
{
    public async Task<int> Purge(int count, string grainPrimaryKey)
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>(grainPrimaryKey).ListAsync();
        var toRemove = ids.Take(count).ToArray();
        int purged = 0;
        foreach (var id in toRemove)
            if (await grains.GetGrain<IReminderWorkerGrainWithState>(id).UnregisterReminderAsync())
                purged++;
        return purged;
    }
}
