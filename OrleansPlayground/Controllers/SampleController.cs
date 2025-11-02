using Microsoft.AspNetCore.Mvc;
using OrleansPlayground.Grains;

namespace OrleansPlayground.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SampleController(IGrainFactory grainFactory) : ControllerBase
{
    [HttpGet("total")]
    public async Task<IActionResult> GetCurrentTotal()
    {
        var grain = grainFactory.GetGrain<ISampleGrain>("demo-calculator");
        var result = await grain.CurrentTotal();

        return Ok(result);
    }

    [HttpPost("add/{toAdd:int}")]
    public async Task<IActionResult> Add([FromRoute] int toAdd)
    {
        var grain = grainFactory.GetGrain<ISampleGrain>("demo-calculator");
        var result = await grain.AddAsync(toAdd);

        return Ok(result);
    }

    [HttpPost("substract/{toRemove:int}")]
    public async Task<IActionResult> Substract([FromRoute] int toRemove)
    {
        var grain = grainFactory.GetGrain<ISampleGrain>("demo-calculator");
        var result = await grain.SubstractAsync(toRemove);

        return Ok(result);
    }

    [HttpPost("simulate-busy")]
    public async Task<IActionResult> SimulateBusy()
    {
        var grain = grainFactory.GetGrain<ISampleGrain>("demo-calculator");
        await grain.SimulateBusy();
        return Ok();
    }
}
