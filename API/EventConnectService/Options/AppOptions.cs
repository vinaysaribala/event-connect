namespace EventConnectService.Options;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Base URL of the UI; links in emails point here.</summary>
    public string BaseUrl { get; set; } = "";
}
