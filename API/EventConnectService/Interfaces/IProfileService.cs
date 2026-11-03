using System.Security.Claims;
using EventConnectService.ViewModels;

namespace EventConnectService.Interfaces;

public interface IProfileService
{
    Task<ProfileViewModel?> GetAsync(ClaimsPrincipal principal);
    Task<ServiceResult<ProfileViewModel>> UpdateAsync(ClaimsPrincipal principal, UpdateProfileViewModel model);
}
