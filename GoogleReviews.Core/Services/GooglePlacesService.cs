using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace GoogleReviews.Core.Services;

public class GooglePlacesService : IReviewService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GooglePlacesService> _logger;
    private readonly string _apiKey;
    private const string PlacesApiBaseUrl = "https://places.googleapis.com/v1/places";

    public GooglePlacesService(IConfiguration configuration, ILogger<GooglePlacesService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _apiKey = configuration["GoogleApi:ApiKey"] ?? throw new ArgumentException("Google API key not configured");
        _httpClient = httpClient;
    }

    public async Task<CompanyReviewData?> GetReviewsAsync(Company company)
    {
        try
        {
            if (string.IsNullOrEmpty(company.PlaceId))
            {
                _logger.LogWarning("No PlaceId provided for company {CompanyName}", company.Name);
                return null;
            }

            var url = $"{PlacesApiBaseUrl}/{company.PlaceId}?fields=displayName,rating,userRatingCount,reviews&key={_apiKey}";
            
            _logger.LogInformation("Fetching reviews for {CompanyName} from Google Places API (New)", company.Name);
            _logger.LogDebug("API URL: {Url}", url.Replace(_apiKey, "***API_KEY***"));
            
            var response = await _httpClient.GetStringAsync(url);
            _logger.LogDebug("Raw API response: {Response}", response);
            
            // Check for error in response
            if (response.Contains("\"error\""))
            {
                _logger.LogWarning("API error response for company {CompanyName} with PlaceId {PlaceId}. Response: {Response}", 
                    company.Name, company.PlaceId, response);
                return null;
            }
            
            var apiResponse = JsonConvert.DeserializeObject<NewPlacesApiResponse>(response);

            if (apiResponse == null)
            {
                _logger.LogWarning("Failed to deserialize API response for company {CompanyName} with PlaceId {PlaceId}. Response: {Response}", 
                    company.Name, company.PlaceId, response);
                return null;
            }

            var reviews = new List<Review>();
            
            _logger.LogInformation("API Success! Company: {CompanyName}, Rating: {Rating}, Total Reviews: {TotalReviews}", 
                company.Name, apiResponse.Rating, apiResponse.UserRatingCount);
            
            if (apiResponse.Reviews != null)
            {
                _logger.LogInformation("Found {ReviewCount} individual reviews", apiResponse.Reviews.Count);
                
                reviews = apiResponse.Reviews.Select(r => new Review
                {
                    Id = $"{company.Id}_{r.PublishTime}_{r.AuthorAttribution?.DisplayName?.Replace(" ", "")}",
                    CompanyId = company.Id,
                    AuthorName = r.AuthorAttribution?.DisplayName ?? "Anonymous",
                    Rating = r.Rating,
                    Text = r.Text?.Text ?? r.OriginalText?.Text,
                    Time = ParseNewApiDateTime(r.PublishTime),
                    AuthorUrl = r.AuthorAttribution?.Uri,
                    ProfilePhotoUrl = r.AuthorAttribution?.PhotoUri
                }).ToList();
                
                // Log first review as example
                if (reviews.Any())
                {
                    var firstReview = reviews.First();
                    _logger.LogInformation("Sample review: {Stars}⭐ by {Author} on {Date}: {Text}", 
                        firstReview.Rating, firstReview.AuthorName, firstReview.Time.ToString("yyyy-MM-dd"), 
                        firstReview.Text?.Substring(0, Math.Min(100, firstReview.Text?.Length ?? 0)) + "...");
                }
            }
            else
            {
                _logger.LogWarning("No individual reviews returned by API");
            }

            var reviewData = new CompanyReviewData
            {
                CompanyId = company.Id,
                CompanyName = apiResponse.DisplayName?.Text ?? company.Name,
                Rating = apiResponse.Rating,
                UserRatingsTotalCount = apiResponse.UserRatingCount,
                Reviews = reviews,
                LastUpdated = DateTime.UtcNow
            };
            
            _logger.LogInformation("Created CompanyReviewData for {CompanyName}: Rating={Rating}, TotalCount={Total}, IndividualReviews={Count}", 
                reviewData.CompanyName, reviewData.Rating, reviewData.UserRatingsTotalCount, reviewData.Reviews.Count);
                
            return reviewData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reviews for company {CompanyName}", company.Name);
            return null;
        }
    }

    public async Task<List<ReviewChange>> DetectChangesAsync(List<Company> companies)
    {
        var changes = new List<ReviewChange>();

        foreach (var company in companies.Where(c => c.IsActive))
        {
            try
            {
                var currentData = await GetReviewsAsync(company);
                if (currentData == null) continue;

                // Create a simple file-based storage for this method
                var dataStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Reviews");
                if (!Directory.Exists(dataStoragePath))
                {
                    Directory.CreateDirectory(dataStoragePath);
                }

                var historicalDataFile = Path.Combine(dataStoragePath, $"{company.Id}.json");
                CompanyReviewData? historicalData = null;

                if (File.Exists(historicalDataFile))
                {
                    var json = await File.ReadAllTextAsync(historicalDataFile);
                    historicalData = JsonConvert.DeserializeObject<CompanyReviewData>(json);
                }
                
                if (historicalData != null)
                {
                    var change = CompareReviewData(historicalData, currentData);
                    if (change != null)
                    {
                        changes.Add(change);
                    }
                }

                // Save current data as historical data
                var currentDataJson = JsonConvert.SerializeObject(currentData, Formatting.Indented);
                await File.WriteAllTextAsync(historicalDataFile, currentDataJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing company {CompanyName}", company.Name);
            }
        }

        return changes;
    }

    private static ReviewChange? CompareReviewData(CompanyReviewData historical, CompanyReviewData current)
    {
        var hasRatingChanged = Math.Abs(historical.Rating - current.Rating) > 0.01;
        var hasTotalCountChanged = historical.UserRatingsTotalCount != current.UserRatingsTotalCount;

        if (!hasRatingChanged && !hasTotalCountChanged) return null;

        var newReviews = current.Reviews
            .Where(cr => !historical.Reviews.Any(hr => hr.Id == cr.Id))
            .OrderByDescending(r => r.Time)
            .ToList();

        var changeType = hasRatingChanged && newReviews.Any() ? ChangeType.Both :
                        hasRatingChanged ? ChangeType.RatingChanged :
                        ChangeType.NewReviews;

        return new ReviewChange
        {
            CompanyId = current.CompanyId,
            CompanyName = current.CompanyName,
            PreviousRating = historical.Rating,
            CurrentRating = current.Rating,
            PreviousTotalReviews = historical.UserRatingsTotalCount,
            CurrentTotalReviews = current.UserRatingsTotalCount,
            NewReviews = newReviews,
            ChangeType = changeType
        };
    }

    private static DateTime ParseNewApiDateTime(string publishTime)
    {
        try
        {
            // New API returns ISO 8601 format like "2023-12-01T10:30:00Z"
            if (DateTime.TryParse(publishTime, out var parsedDate))
            {
                return parsedDate.ToUniversalTime();
            }
        }
        catch
        {
            // Fall back to current time if parsing fails
        }
        
        return DateTime.UtcNow;
    }

    public void Dispose()
    {
        // HttpClient is managed by DI container, don't dispose it here
    }
}