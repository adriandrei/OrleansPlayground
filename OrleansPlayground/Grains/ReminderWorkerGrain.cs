using Orleans.Timers;

namespace OrleansPlayground.Grains;

public interface IReminderWorkerGrain : IGrainWithStringKey
{
    Task EnsureRegisteredAsync(TimeSpan? due = null, TimeSpan? period = null);
    Task<bool> UnregisterAsync();
    Task<bool> ReRegisterAsync();  // new
}

public sealed class ReminderWorkerGrain(
    ILogger<ReminderWorkerGrain> logger,
    IReminderRegistry registry,
    IGrainFactory grains)
    : Grain, IReminderWorkerGrain, IRemindable
{
    private const string ReminderName = "periodic";
    private static readonly TimeSpan DefaultDue = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(10);

    public async Task EnsureRegisteredAsync(TimeSpan? due = null, TimeSpan? period = null)
    {
        await registry.RegisterOrUpdateReminder(
            this.GetGrainId(),
            ReminderName,
            due ?? DefaultDue,
            period ?? DefaultPeriod);

        await grains.GetGrain<IWorkerCatalogGrain>("catalog")
                    .AddAsync(this.GetPrimaryKeyString());
    }

    public async Task<bool> UnregisterAsync()
    {
        var r = await registry.GetReminder(this.GetGrainId(), ReminderName);
        if (r is not null)
        {
            await registry.UnregisterReminder(this.GetGrainId(), r);
            await grains.GetGrain<IWorkerCatalogGrain>("catalog")
                        .RemoveAsync(this.GetPrimaryKeyString());

            DeactivateOnIdle();
            return true;
        }
        return false;
    }

    public async Task<bool> ReRegisterAsync()
    {
        DeactivateOnIdle();
        return true;
    }

    public async Task ReceiveReminder(string name, TickStatus status)
    {
        try
        {
            //await Task.Delay(Random.Shared.Next(20, 80));
            logger.LogInformation("Grain {Id} ticked at {UtcNow:O}", this.GetPrimaryKeyString(), DateTime.UtcNow);

            MigrateOnIdle();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing reminder tick");
        }
    }
}
