using Orleans.Timers;

namespace OrleansPlayground.Grains;

public interface IReminderWorkerGrain : IMyGrain { }
public sealed class RemidnerWorkerGrain(
    ILogger<ReminderWorkerGrainWithState> logger,
    IReminderRegistry registry,
    IGrainFactory grains,
    ILocalSiloDetails siloDetails)
    : Grain, IReminderWorkerGrain, IRemindable
{
    private const string ReminderName = "reminder";
    private readonly string _grainType = nameof(ReminderWorkerGrainWithState);

    public override async Task OnActivateAsync(CancellationToken token)
    {
        logger.LogInformation(
            "[Activation] {GrainType} activated. GrainId={GrainId}, Silo={Silo}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            siloDetails.Name,
            DateTime.UtcNow);

        await base.OnActivateAsync(token);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken token)
    {
        logger.LogInformation(
            "[Deactivation] {GrainType} deactivated. GrainId={GrainId}, Reason={Reason}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            reason.Description,
            DateTime.UtcNow);

        await base.OnDeactivateAsync(reason, token);
    }

    public async Task RegisterReminderAsync(TimeSpan due, TimeSpan period)
    {
        await registry.RegisterOrUpdateReminder(
            this.GetGrainId(),
            ReminderName,
            due,
            period);

        await grains.GetGrain<IWorkerCatalogGrain>("stateless-catalog")
                    .AddAsync(this.GetPrimaryKeyString());
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        await Task.Delay(
            Random.Shared.Next(5, 20) > 18 ?
                Random.Shared.Next(800, 3200) :
                Random.Shared.Next(50, 800));

        MigrateOnIdle();
    }

    public async Task<bool> UnregisterReminderAsync()
    {
        var r = await registry.GetReminder(this.GetGrainId(), ReminderName);
        if (r is not null)
        {
            await registry.UnregisterReminder(this.GetGrainId(), r);
            await grains.GetGrain<IWorkerCatalogGrain>("stateless-catalog")
                        .RemoveAsync(this.GetPrimaryKeyString());

            DeactivateOnIdle();
            return true;
        }

        return false;
    }
}

