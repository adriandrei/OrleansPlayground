namespace OrleansPlayground;

[GenerateSerializer]
public sealed class WorkerCatalogState
{
    [Id(0)] public HashSet<string> Ids { get; set; } = new();
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

