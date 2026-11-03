namespace EventConnectService.Implementations;

internal static class TimeZones
{
    /// <summary>Accepts IANA ids (America/Chicago) and rejects Windows ids (Central Standard Time).</summary>
    public static bool IsIana(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId;

    /// <summary>
    /// Converts a local date-time in the IANA zone <paramref name="timeZone"/> to UTC. A time in a DST gap moves
    /// forward by the gap length; a time in a DST overlap resolves to the earlier of its two instants.
    /// </summary>
    public static DateTime ToUtc(DateTime local, string timeZone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        TimeSpan offset;
        if (zone.IsInvalidTime(local))
        {
            // The offset in force before the gap, e.g. 02:30 EST (UTC-5) on spring-forward night = 03:30 EDT.
            offset = zone.GetUtcOffset(local.AddDays(-1));
        }
        else if (zone.IsAmbiguousTime(local))
        {
            // The larger offset is the earlier instant (daylight time, before the clocks go back).
            offset = zone.GetAmbiguousTimeOffsets(local).Max();
        }
        else
        {
            offset = zone.GetUtcOffset(local);
        }

        return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
    }

    /// <summary>The local wall-clock time in <paramref name="timeZone"/> for a UTC instant.</summary>
    public static DateTime ToLocal(DateTime utc, string timeZone) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(timeZone)),
            DateTimeKind.Unspecified);
}
