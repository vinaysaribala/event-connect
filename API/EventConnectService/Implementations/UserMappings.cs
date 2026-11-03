using EventConnectCore.Models;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace EventConnectService.Implementations;

internal static class UserMappings
{
    public static async Task<UserViewModel> ToUserViewModelAsync(this UserManager<User> userManager, User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserViewModel(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, [.. roles.Order()]);
    }

    public static async Task<ProfileViewModel> ToProfileViewModelAsync(this UserManager<User> userManager, User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new ProfileViewModel(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, [.. roles.Order()], user.TimeZone);
    }
}
