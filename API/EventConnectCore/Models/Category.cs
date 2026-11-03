namespace EventConnectCore.Models;

/// <summary>Event and template category, shared by events (02) and templates (03); managed by admins (09).</summary>
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
