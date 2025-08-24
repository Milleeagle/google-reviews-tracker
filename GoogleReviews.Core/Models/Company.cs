namespace GoogleReviews.Core.Models;

public class Company
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlaceId { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}