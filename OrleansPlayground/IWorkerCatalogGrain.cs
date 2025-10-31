namespace OrleansPlayground;

public interface IWorkerCatalogGrain : IGrainWithStringKey
{
    Task AddAsync(string id);
    Task RemoveAsync(string id);
    Task<IReadOnlyCollection<string>> ListAsync();
}
