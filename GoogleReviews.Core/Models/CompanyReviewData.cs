namespace GoogleReviews.Core.Models;

public class CompanyReviewData
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int UserRatingsTotalCount { get; set; }
    public List<Review> Reviews { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}