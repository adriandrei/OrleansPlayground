namespace OrleansPlayground.Grains;

[GenerateSerializer]
public sealed class WorkerCatalogState
{
    [Id(0)] public HashSet<string> Ids { get; set; } = new();
}

public interface IWorkerCatalogGrain : IGrainWithStringKey
{
    Task AddAsync(string id);
    Task RemoveAsync(string id);
    Task<IReadOnlyCollection<string>> ListAsync();
}

public sealed class WorkerCatalogGrain(
    [PersistentState("catalog", "catalogStore")] IPersistentState<WorkerCatalogState> state)
    : Grain, IWorkerCatalogGrain
{
    public async Task AddAsync(string id)
    {
        if (state.State.Ids.Add(id))
            await state.WriteStateAsync();
    }

    public async Task RemoveAsync(string id)
    {
        if (state.State.Ids.Remove(id))
            await state.WriteStateAsync();
    }

    public Task<IReadOnlyCollection<string>> ListAsync()
        => Task.FromResult<IReadOnlyCollection<string>>(state.State.Ids.ToArray());
}

