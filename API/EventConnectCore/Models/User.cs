using Microsoft.AspNetCore.Identity;

namespace EventConnectCore.Models;

public class User : IdentityUser<int>
{
    public DateTime? EmailVerifiedAt { get; set; }
    public required string DisplayName { get; set; }
    public required string TimeZone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
