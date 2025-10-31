﻿using Orleans.Timers;
using System.Diagnostics;

namespace OrleansPlayground.Grains;

[GenerateSerializer]
public sealed class ReminderWorkerState
{
    [Id(0)] public int TotalTicks { get; set; }
    [Id(1)] public DateTime? FirstTick { get; set; }
    [Id(2)] public DateTime? LastTick { get; set; }
    [Id(3)] public Dictionary<string, int> PerSiloCounts { get; set; } = new();
    [Id(4)] public Dictionary<string, double> PerSiloDelayTotals { get; set; } = new();
    [Id(5)] public double TotalDelayMs { get; set; }
}

public interface IReminderWorkerGrain : IGrainWithStringKey
{
    Task EnsureRegisteredAsync(TimeSpan due, TimeSpan period);
    Task<bool> UnregisterAsync();
    Task<bool> ReRegisterAsync();  // new
    Task<ReminderWorkerStats> GetStatsAsync();
}

[GenerateSerializer]
public record ReminderWorkerStats(
    string GrainId,
    int TotalTicks,
    double AverageDelayMs,
    DateTime? FirstTick,
    DateTime? LastTick,
    Dictionary<string, int> PerSiloCounts,
    Dictionary<string, double> PerSiloAverageDelays);

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

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken token)
    {
        await state.WriteStateAsync();
        logger.LogInformation(
            "[Deactivation] {GrainType} deactivated. GrainId={GrainId}, Silo={Silo}, Reason={Reason}, Time={Time:O}",
            _grainType,
            this.GetPrimaryKeyString(),
            _lastSiloName ?? siloDetails.Name,
            reason.Description,
            DateTime.UtcNow);

        await base.OnDeactivateAsync(reason, token);
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
        var expected = status.CurrentTickTime;
        var delayMs = (now - expected).TotalMilliseconds;
        var silo = siloDetails.Name;

        // initialize
        state.State.FirstTick ??= now;
        state.State.LastTick = now;
        state.State.TotalTicks++;
        state.State.TotalDelayMs += delayMs;

        // per-silo stats
        state.State.PerSiloCounts[silo] = state.State.PerSiloCounts.TryGetValue(silo, out var c) ? c + 1 : 1;
        state.State.PerSiloDelayTotals[silo] =
            state.State.PerSiloDelayTotals.TryGetValue(silo, out var d) ? d + delayMs : delayMs;

        logger.LogInformation(
            "[Tick] Grain={Id}, Count={Count}, Delay={Delay:F1}ms, Silo={Silo}, Time={Time:O}",
            this.GetPrimaryKeyString(),
            state.State.TotalTicks,
            delayMs,
            silo,
            now);
    }

    public Task<ReminderWorkerStats> GetStatsAsync()
    {
        var avgDelay = state.State.TotalTicks > 0
            ? state.State.TotalDelayMs / state.State.TotalTicks
            : 0.0;

        var siloAverages = state.State.PerSiloCounts
            .ToDictionary(kv => kv.Key,
                          kv => state.State.PerSiloDelayTotals[kv.Key] / kv.Value);

        return Task.FromResult(new ReminderWorkerStats(
            this.GetPrimaryKeyString(),
            state.State.TotalTicks,
            avgDelay,
            state.State.FirstTick,
            state.State.LastTick,
            new Dictionary<string, int>(state.State.PerSiloCounts),
            siloAverages));
    }
}

