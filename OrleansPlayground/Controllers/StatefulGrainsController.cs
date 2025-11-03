using Microsoft.AspNetCore.Mvc;
using OrleansPlayground.Grains;

namespace OrleansPlayground.Controllers;

[ApiController]
[Route("stateful-grains")]
public sealed class StatefulGrainsController(IGrainFactory grains) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromQuery] int count = 1)
    {
        const int BatchSize = 50; // Tune this depending on silo count & CPU
        var allIds = Enumerable.Range(0, count)
            .Select(_ => Guid.NewGuid().ToString("N"))
            .ToArray();

        for (int i = 0; i < allIds.Length; i += BatchSize)
        {
            var batch = allIds.Skip(i).Take(BatchSize).ToArray();

            var tasks = batch.Select(id =>
                grains.GetGrain<IReminderWorkerGrainWithState>(id)
                      .RegisterReminderAsync(Configuration.ReminderDue, Configuration.ReminderPeriod));

            await Task.WhenAll(tasks);

            Console.WriteLine($"Registered batch {i / BatchSize + 1} ({batch.Length} grains)");
        }

        return Ok();
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("stateful-catalog").ListAsync();
        return Ok(ids);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("stateful-catalog").ListAsync();
        return Ok(ids.Count);
    }

    [HttpPost("purge")]
    public async Task<IActionResult> Purge([FromQuery] int count = 0)
    {
        var maint = grains.GetGrain<IRemindersMaintenanceGrain>("maintenance");
        var purged = await maint.Purge<IReminderWorkerGrainWithState>(count, "stateful-catalog");
        return Ok(new { Purged = purged });
    }

    [HttpPost("rebalance")]
    public async Task<IActionResult> Rebalance([FromQuery] int count = 0)
    {
        for (int i = 0; i < count; i++)
        {
            var id = Guid.NewGuid().ToString("N");
            await grains.GetGrain<IReminderWorkerGrainWithState>(id)
                        .RegisterReminderAsync(Configuration.ReminderDue, Configuration.ReminderPeriod);
        }
        return Ok(new { Registered = count });
    }

    [HttpGet("reminder-cluster-stats")]
    public async Task<IActionResult> ReminderClusterStats()
    {
        var stats = await grains.GetGrain<IReminderStatsGrain>("stats").GetClusterStatsAsync();
        return Ok(stats);
    }
}