using Microsoft.AspNetCore.Mvc;

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
                        .EnsureRegisteredAsync();
        }
        return Ok(new { Registered = count });
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("catalog").ListAsync();
        return Ok(ids);
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
        foreach (var id in toRebalance)
        {
            await grains.GetGrain<IReminderWorkerGrain>(id).ReRegisterAsync();
            rebalanced++;
        }

        return Ok(new { Rebalanced = rebalanced });
    }

}
