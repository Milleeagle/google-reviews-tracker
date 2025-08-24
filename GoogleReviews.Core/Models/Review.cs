namespace GoogleReviews.Core.Models;

public class Review
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Text { get; set; }
    public DateTime Time { get; set; }
    public string? AuthorUrl { get; set; }
    public string? ProfilePhotoUrl { get; set; }
}