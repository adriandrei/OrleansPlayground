namespace OrleansPlayground.Grains;

[GenerateSerializer]
public sealed class SampleGrainState
{
    [Id(0)] public int Value { get; set; }
}

public interface ISampleGrain : IGrainWithStringKey
{
    Task<int> AddAsync(int value);
    Task<int> SubstractAsync(int value);
    Task<int> CurrentTotal();
    Task SimulateBusy();
}

public class SampleGrain(
    [PersistentState("sample", "catalogStore")] IPersistentState<SampleGrainState> state,
    ILogger<SampleGrain> logger
    ) : Grain, ISampleGrain
{

    override public Task OnActivateAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SampleGrain activated. GrainId={GrainId}, PrimaryKeyString=Time={Time:O}",
            this.GetPrimaryKeyString(),
            DateTime.UtcNow);
        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        logger.LogInformation("SampleGrain deactivated. GrainId={GrainId}, Reason={Reason}, Time={Time:O}",
            this.GetPrimaryKeyString(),
            reason,
            DateTime.UtcNow);
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<int> AddAsync(int value)
    {
        state.State.Value += value;
        await state.WriteStateAsync();

        logger.LogInformation("Current value: {Value}", state.State.Value);
        return state.State.Value;
    }

    public async Task<int> SubstractAsync(int value)
    {
        state.State.Value -= value;
        await state.WriteStateAsync();

        logger.LogInformation("Current value: {Value}", state.State.Value);
        return state.State.Value;
    }

    public Task<int> CurrentTotal()
    {
        return Task.FromResult(state.State.Value);
    }

    public async Task SimulateBusy()
    {
        await Task.Delay(TimeSpan.FromSeconds(40));
    }
}
