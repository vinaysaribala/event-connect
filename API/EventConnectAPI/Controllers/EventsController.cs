using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EventConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventViewModel>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await eventService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventViewModel>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EventViewModel>> Create(CreateEventViewModel model, CancellationToken cancellationToken)
    {
        var created = await eventService.CreateAsync(model, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
