namespace OrleansPlayground;

public interface IReminderWorkerGrain : IGrainWithStringKey
{
    Task EnsureRegisteredAsync(TimeSpan? due = null, TimeSpan? period = null);
    Task<bool> UnregisterAsync();
    Task<bool> ReRegisterAsync();  // new
}
