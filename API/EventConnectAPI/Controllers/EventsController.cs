using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EventConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventViewModel>>> List([FromQuery] EventQueryViewModel query, CancellationToken cancellationToken)
    {
        return Ok(await eventService.ListAsync(User, query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventViewModel>> Get(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.GetAsync(User, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventViewModel>> Create(SaveEventViewModel model, CancellationToken cancellationToken)
    {
        var result = await eventService.CreateAsync(User, model, cancellationToken);
        return result.Succeeded ? Created($"/api/events/{result.Value!.Id}", result.Value) : this.Failure(result.Outcome);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventViewModel>> Update(int id, UpdateEventViewModel model, CancellationToken cancellationToken)
    {
        var result = await eventService.UpdateAsync(User, id, model, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.Failure(result.Outcome);
    }

    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventViewModel>> Publish(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.PublishAsync(User, id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.Failure(result.Outcome);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventViewModel>> Cancel(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelEventViewModel? model,
        CancellationToken cancellationToken)
    {
        var result = await eventService.CancelAsync(User, id, model ?? new CancelEventViewModel(null), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.Failure(result.Outcome);
    }

    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventViewModel>> Archive(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.ArchiveAsync(User, id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.Failure(result.Outcome);
    }

    [HttpPost("{id:int}/duplicate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventViewModel>> Duplicate(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.DuplicateAsync(User, id, cancellationToken);
        return result.Succeeded ? Created($"/api/events/{result.Value!.Id}", result.Value) : this.Failure(result.Outcome);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await eventService.DeleteAsync(User, id, cancellationToken);
        return result.Succeeded ? NoContent() : this.Failure(result);
    }
}
