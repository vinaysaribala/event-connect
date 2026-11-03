using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileViewModel>> Get()
    {
        var profile = await profileService.GetAsync(User);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<ProfileViewModel>> Update(UpdateProfileViewModel model)
    {
        var result = await profileService.UpdateAsync(User, model);
        return result.Succeeded ? Ok(result.Value) : this.Failure(result.Outcome);
    }
}
