namespace GoogleReviews.Core.Models;

public class ReviewChange
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public double? PreviousRating { get; set; }
    public double CurrentRating { get; set; }
    public int? PreviousTotalReviews { get; set; }
    public int CurrentTotalReviews { get; set; }
    public List<Review> NewReviews { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public ChangeType ChangeType { get; set; }
}

public enum ChangeType
{
    RatingChanged,
    NewReviews,
    Both
}