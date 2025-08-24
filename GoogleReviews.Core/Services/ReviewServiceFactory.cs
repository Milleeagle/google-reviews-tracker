using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoogleReviews.Core.Services;

public static class ReviewServiceFactory
{
    public static IReviewService CreateReviewService(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var serviceType = configuration["ReviewService:Type"]?.ToLowerInvariant() ?? "api";

        return serviceType switch
        {
            "scraping" => serviceProvider.GetRequiredService<GoogleMapsScrapeService>(),
            "api" => serviceProvider.GetRequiredService<GooglePlacesService>(),
            _ => throw new InvalidOperationException($"Unknown review service type: {serviceType}")
        };
    }
}

public class DynamicReviewService : IReviewService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicReviewService> _logger;

    public DynamicReviewService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DynamicReviewService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CompanyReviewData?> GetReviewsAsync(Company company)
    {
        var service = GetCurrentService();
        _logger.LogInformation("Using {ServiceType} for reviews", service.GetType().Name);
        return await service.GetReviewsAsync(company);
    }

    public async Task<List<ReviewChange>> DetectChangesAsync(List<Company> companies)
    {
        var service = GetCurrentService();
        _logger.LogInformation("Using {ServiceType} for change detection", service.GetType().Name);
        return await service.DetectChangesAsync(companies);
    }

    private IReviewService GetCurrentService()
    {
        var serviceType = _configuration["ReviewService:Type"]?.ToLowerInvariant() ?? "api";

        return serviceType switch
        {
            "scraping" => _serviceProvider.GetRequiredService<GoogleMapsScrapeService>(),
            "api" => _serviceProvider.GetRequiredService<GooglePlacesService>(),
            _ => throw new InvalidOperationException($"Unknown review service type: {serviceType}. Use 'API' or 'Scraping'")
        };
    }
}