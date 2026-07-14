using BreedersTestTask.DTOs;
using BreedersTestTask.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace BreedersTestTask.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LittersController : ControllerBase
{
    private readonly ILitterService _litterService;

    public LittersController(ILitterService litterService)
    {
        _litterService = litterService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LitterDto>>> GetLitters([FromQuery] GetLittersQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _litterService.GetLittersAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<LitterDto>> Publish(int id, CancellationToken cancellationToken = default)
    {
        var result = await _litterService.PublishAsync(id, cancellationToken);
        return Ok(result);
    }
}