using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EventConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryViewModel>>> GetActive(CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetActiveAsync(cancellationToken));
    }
}
