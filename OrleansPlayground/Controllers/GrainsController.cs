using Microsoft.AspNetCore.Mvc;
using OrleansPlayground.Grains;

namespace OrleansPlayground.Controllers;

[ApiController]
[Route("grains")]
public sealed class GrainsController(IGrainFactory grains) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromQuery] int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            var id = Guid.NewGuid().ToString("N");
            await grains.GetGrain<IReminderWorkerGrain>(id)
                        .EnsureRegisteredAsync(Configuration.ReminderDue, Configuration.ReminderPeriod);
        }
        return Ok(new { Registered = count });
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("catalog").ListAsync();
        return Ok(ids);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("catalog").ListAsync();
        return Ok(ids.Count);
    }

    [HttpPost("purge")]
    public async Task<IActionResult> Purge([FromQuery] int count = 0)
    {
        var maint = grains.GetGrain<IRemindersMaintenanceGrain>("maintenance");
        var purged = await maint.PurgeAsync(count);
        return Ok(new { Purged = purged });
    }

    [HttpPost("rebalance")]
    public async Task<IActionResult> Rebalance([FromQuery] int count = 0)
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("catalog").ListAsync();
        var toRebalance = ids.Take(count > 0 ? count : ids.Count).ToArray();

        int rebalanced = 0;
        foreach (var batch in toRebalance.Chunk(50))
        {
            await Task.WhenAll(batch.Select(id =>
                grains.GetGrain<IReminderWorkerGrain>(id).ReRegisterAsync()));
            rebalanced += batch.Length;

            await Task.Delay(100);
        }

        return Ok(new { Rebalanced = rebalanced });
    }

    [HttpGet("reminder-cluster-stats")]
    public async Task<IActionResult> ReminderClusterStats()
    {
        var stats = await grains.GetGrain<IReminderStatsGrain>("stats").GetClusterStatsAsync();
        return Ok(stats);
    }

}