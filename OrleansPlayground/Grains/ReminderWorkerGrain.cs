using Orleans.Timers;
using System.Diagnostics;

namespace OrleansPlayground.Grains;

public interface IReminderWorkerGrain : IGrainWithStringKey
{
    Task EnsureRegisteredAsync(TimeSpan due, TimeSpan perio);
    Task<bool> UnregisterAsync();
    Task<bool> ReRegisterAsync();  // new
    Task<ReminderWorkerStats> GetStatsAsync();
}

[GenerateSerializer]
public sealed class ReminderWorkerState
{
    [Id(0)] public int TickCount { get; set; }
    [Id(1)] public DateTime? FirstTick { get; set; }
    [Id(2)] public DateTime? LastTick { get; set; }
    [Id(3)] public string? LastSilo { get; set; }
}

[GenerateSerializer]
public record ReminderWorkerStats(
    string GrainId,
    int TickCount,
    DateTime? FirstTick,
    DateTime? LastTick,
    string LastSilo);



public sealed class ReminderWorkerGrain(
    ILogger<ReminderWorkerGrain> logger,
    IReminderRegistry registry,
    IGrainFactory grains,
    ILocalSiloDetails siloDetails,
    [PersistentState("worker", "catalogStore")] IPersistentState<ReminderWorkerState> state)
    : Grain, IReminderWorkerGrain, IRemindable
{
    private const string ReminderName = "periodic";
    private string? _lastSiloName;
    private readonly string _grainType = nameof(ReminderWorkerGrain);

    public override async Task OnActivateAsync(CancellationToken token)
    {
        logger.LogInformation(
            "[Activation] {GrainType} activated. GrainId={GrainId}, Silo={Silo}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            siloDetails.Name,
            DateTime.UtcNow);

        _lastSiloName = siloDetails.Name;
        await base.OnActivateAsync(token);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken token)
    {
        logger.LogInformation(
            "[Deactivation] {GrainType} deactivated. GrainId={GrainId}, Silo={Silo}, Reason={Reason}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            _lastSiloName ?? siloDetails.Name,
            reason.Description,
            DateTime.UtcNow);

        return base.OnDeactivateAsync(reason, token);
    }

    public async Task EnsureRegisteredAsync(TimeSpan due, TimeSpan period)
    {
        await registry.RegisterOrUpdateReminder(
            this.GetGrainId(),
            ReminderName,
            due,
            period);

        await grains.GetGrain<IWorkerCatalogGrain>("catalog")
                    .AddAsync(this.GetPrimaryKeyString());

        logger.LogInformation(
            "[Registration] {GrainType} registered reminder. GrainId={GrainId}, Due={Due}, Period={Period}, Silo={Silo}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            due,
            period,
            siloDetails.Name,
            DateTime.UtcNow);
    }

    public async Task<bool> UnregisterAsync()
    {
        var r = await registry.GetReminder(this.GetGrainId(), ReminderName);
        if (r is not null)
        {
            await registry.UnregisterReminder(this.GetGrainId(), r);
            await grains.GetGrain<IWorkerCatalogGrain>("catalog")
                        .RemoveAsync(this.GetPrimaryKeyString());

            logger.LogInformation(
                "[Unregister] {GrainType} unregistered reminder. GrainId={GrainId}, Silo={Silo}, Time={Time:O}",
                _grainType,
                this.GetPrimaryKeyString(),
                siloDetails.Name,
                DateTime.UtcNow);

            DeactivateOnIdle();
            return true;
        }

        logger.LogWarning(
            "[Unregister] {GrainType} no reminder found to unregister. GrainId={GrainId}, Silo={Silo}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            siloDetails.Name,
            DateTime.UtcNow);

        return false;
    }

    public Task<bool> ReRegisterAsync()
    {
        logger.LogInformation(
            "[ReRegister] {GrainType} re-register requested. GrainId={GrainId}, Silo={Silo}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            siloDetails.Name,
            DateTime.UtcNow);

        MigrateOnIdle();
        return Task.FromResult(true);
    }

    public async Task ReceiveReminder(string name, TickStatus status)
    {
        var now = DateTime.UtcNow;
        state.State.TickCount++;
        state.State.FirstTick ??= now;
        state.State.LastTick = now;
        state.State.LastSilo = siloDetails.Name;

        await state.WriteStateAsync(); // durable save

        logger.LogInformation(
            "[Tick] Grain={Id}, Count={Count}, Silo={Silo}, Time={Time:O}",
            this.GetPrimaryKeyString(),
            state.State.TickCount,
            siloDetails.Name,
            now);

        await Task.Delay(Random.Shared.Next(50, 400)); // simulate work
    }

    public Task<ReminderWorkerStats> GetStatsAsync()
        => Task.FromResult(new ReminderWorkerStats(
            this.GetPrimaryKeyString(),
            state.State.TickCount,
            state.State.FirstTick,
            state.State.LastTick,
            state.State.LastSilo ?? siloDetails.Name));
}

