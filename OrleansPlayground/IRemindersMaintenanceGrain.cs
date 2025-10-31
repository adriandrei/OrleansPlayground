namespace OrleansPlayground;

public interface IRemindersMaintenanceGrain : IGrainWithStringKey
{
    Task<int> PurgeAsync(int count);
}
