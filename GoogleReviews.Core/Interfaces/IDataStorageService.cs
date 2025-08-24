using GoogleReviews.Core.Models;

namespace GoogleReviews.Core.Interfaces;

public interface IDataStorageService
{
    Task<List<Company>> GetCompaniesAsync();
    Task SaveCompaniesAsync(List<Company> companies);
    Task<CompanyReviewData?> GetHistoricalDataAsync(string companyId);
    Task SaveHistoricalDataAsync(CompanyReviewData data);
    Task<List<CompanyReviewData>> GetAllHistoricalDataAsync();
}