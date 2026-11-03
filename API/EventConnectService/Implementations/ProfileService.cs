using System.Security.Claims;
using EventConnectCore.Models;
using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace EventConnectService.Implementations;

public class ProfileService(UserManager<User> userManager) : IProfileService
{
    public async Task<ProfileViewModel?> GetAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null ? null : await userManager.ToProfileViewModelAsync(user);
    }

    public async Task<ServiceResult<ProfileViewModel>> UpdateAsync(ClaimsPrincipal principal, UpdateProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return ServiceResult.Unauthorized();
        }

        var timeZone = model.TimeZone.Trim();
        if (!IsIanaTimeZone(timeZone))
        {
            return ServiceResult.Invalid("TimeZone", "Unknown time zone. Use an IANA name such as America/New_York.");
        }

        user.DisplayName = model.DisplayName.Trim();
        user.TimeZone = timeZone;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return await userManager.ToProfileViewModelAsync(user);
    }

    // Accepts IANA ids (America/Chicago) and rejects Windows ids (Central Standard Time).
    private static bool IsIanaTimeZone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId;
}
