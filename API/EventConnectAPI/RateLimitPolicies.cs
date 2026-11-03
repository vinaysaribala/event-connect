namespace EventConnectAPI;

public static class RateLimitPolicies
{
    /// <summary>Per-IP limit on sign-in, registration and password reset endpoints.</summary>
    public const string Auth = "auth";
}
