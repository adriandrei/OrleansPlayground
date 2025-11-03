using Microsoft.AspNetCore.Mvc;
using OrleansPlayground.Grains;

namespace OrleansPlayground.Controllers;

[ApiController]
[Route("stateless-grains-with-timers")]
public sealed class StelessGrainsWithTimersController(IGrainFactory grains) : ControllerBase
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
                grains.GetGrain<IReminderTimerWorkerGrain>(id)
                      .RegisterReminderAsync(Configuration.ReminderDue, Configuration.ReminderPeriod, Configuration.TimerDue, Configuration.TimerPeriod));

            await Task.WhenAll(tasks);

            Console.WriteLine($"Registered batch {i / BatchSize + 1} ({batch.Length} grains)");
        }

        return Ok();
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("stateless-with-timer-catalog").ListAsync();
        return Ok(ids);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var ids = await grains.GetGrain<IWorkerCatalogGrain>("stateless-with-timer-catalog").ListAsync();
        return Ok(ids.Count);
    }

    [HttpPost("purge")]
    public async Task<IActionResult> Purge([FromQuery] int count = 0)
    {
        var maint = grains.GetGrain<IRemindersMaintenanceGrain>("maintenance");
        var purged = await maint.Purge<IReminderTimerWorkerGrain>(count, "stateless-with-timer-catalog");
        return Ok(new { Purged = purged });
    }
}
