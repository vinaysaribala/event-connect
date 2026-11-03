namespace EventConnectService.Options;

public class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>A user registering or verifying with this email also gets the Administrator role.</summary>
    public string? SeedEmail { get; set; }
}
