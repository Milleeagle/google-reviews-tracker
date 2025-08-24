using GoogleReviews.Core.Models;

namespace GoogleReviews.Core.Interfaces;

public interface IReviewService
{
    Task<CompanyReviewData?> GetReviewsAsync(Company company);
    Task<List<ReviewChange>> DetectChangesAsync(List<Company> companies);
}