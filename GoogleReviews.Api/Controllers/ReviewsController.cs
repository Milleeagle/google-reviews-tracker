using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoogleReviews.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IDataStorageService _dataStorage;
    private readonly IGoogleDocsService _googleDocs;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        IReviewService reviewService,
        IDataStorageService dataStorage,
        IGoogleDocsService googleDocs,
        ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _dataStorage = dataStorage;
        _googleDocs = googleDocs;
        _logger = logger;
    }

    [HttpPost("check")]
    public async Task<IActionResult> TriggerReviewCheck()
    {
        try
        {
            _logger.LogInformation("Manual review check triggered via API");

            var companies = await _dataStorage.GetCompaniesAsync();
            var activeCompanies = companies.Where(c => c.IsActive).ToList();

            _logger.LogInformation("Checking reviews for {Count} active companies: [{Companies}]", 
                activeCompanies.Count, string.Join(", ", activeCompanies.Select(c => $"{c.Name}({c.Id})")));

            // Log each company's details
            foreach (var company in activeCompanies)
            {
                _logger.LogInformation("Active company: {Name}, PlaceId: {PlaceId}, GoogleMapsUrl: {Url}", 
                    company.Name, 
                    string.IsNullOrEmpty(company.PlaceId) ? "[EMPTY]" : company.PlaceId, 
                    string.IsNullOrEmpty(company.GoogleMapsUrl) ? "[EMPTY]" : "SET");
            }

            var changes = await _reviewService.DetectChangesAsync(activeCompanies);
            
            _logger.LogInformation("DetectChangesAsync returned {Count} changes", changes.Count);

            if (changes.Any())
            {
                await _googleDocs.ReportChangesAsync(changes);
                
                _logger.LogInformation("Found {Count} companies with changes, reported to Google Docs", changes.Count);
                
                return Ok(new
                {
                    success = true,
                    message = $"Found changes in {changes.Count} companies. Report sent to Google Docs.",
                    changes = changes.Select(c => new
                    {
                        companyName = c.CompanyName,
                        changeType = c.ChangeType.ToString(),
                        previousRating = c.PreviousRating,
                        currentRating = c.CurrentRating,
                        newReviewsCount = c.NewReviews.Count
                    })
                });
            }
            else
            {
                _logger.LogInformation("No changes detected");
                
                return Ok(new
                {
                    success = true,
                    message = "No changes detected in any company reviews.",
                    changes = Array.Empty<object>()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual review check");
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred during the review check.",
                error = ex.Message
            });
        }
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        try
        {
            var companies = await _dataStorage.GetCompaniesAsync();
            
            return Ok(new
            {
                success = true,
                companies = companies.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    placeId = c.PlaceId,
                    googleMapsUrl = c.GoogleMapsUrl,
                    isActive = c.IsActive,
                    lastUpdated = c.LastUpdated
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving companies");
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while retrieving companies.",
                error = ex.Message
            });
        }
    }

    [HttpGet("companies/{companyId}/reviews")]
    public async Task<IActionResult> GetCompanyReviews(string companyId)
    {
        try
        {
            var companies = await _dataStorage.GetCompaniesAsync();
            var company = companies.FirstOrDefault(c => c.Id == companyId);
            
            if (company == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Company with ID '{companyId}' not found."
                });
            }

            var reviewData = await _reviewService.GetReviewsAsync(company);
            
            if (reviewData == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "No review data available for this company.",
                    data = (object?)null
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    companyId = reviewData.CompanyId,
                    companyName = reviewData.CompanyName,
                    rating = reviewData.Rating,
                    totalReviews = reviewData.UserRatingsTotalCount,
                    lastUpdated = reviewData.LastUpdated,
                    reviews = reviewData.Reviews.Select(r => new
                    {
                        id = r.Id,
                        authorName = r.AuthorName,
                        rating = r.Rating,
                        text = r.Text,
                        time = r.Time,
                        authorUrl = r.AuthorUrl
                    }).OrderByDescending(r => r.time).Take(10)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for company {CompanyId}", companyId);
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while retrieving company reviews.",
                error = ex.Message
            });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var companies = await _dataStorage.GetCompaniesAsync();
            var activeCompanies = companies.Count(c => c.IsActive);
            var historicalData = await _dataStorage.GetAllHistoricalDataAsync();

            return Ok(new
            {
                success = true,
                status = new
                {
                    totalCompanies = companies.Count,
                    activeCompanies = activeCompanies,
                    companiesWithData = historicalData.Count,
                    lastDataUpdate = historicalData.Any() 
                        ? historicalData.Max(h => h.LastUpdated) 
                        : (DateTime?)null,
                    serverTime = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status");
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while retrieving status.",
                error = ex.Message
            });
        }
    }

    [HttpPost("companies")]
    public async Task<IActionResult> AddCompany([FromBody] AddCompanyRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Company name is required." });
            }

            var companies = await _dataStorage.GetCompaniesAsync();
            
            // Generate ID from name
            var id = GenerateCompanyId(request.Name);
            
            // Check if company already exists
            if (companies.Any(c => c.Id == id))
            {
                return Conflict(new { success = false, message = "A company with this name already exists." });
            }

            var newCompany = new Company
            {
                Id = id,
                Name = request.Name.Trim(),
                PlaceId = request.PlaceId?.Trim() ?? "",
                GoogleMapsUrl = request.GoogleMapsUrl?.Trim(),
                IsActive = request.IsActive,
                LastUpdated = DateTime.UtcNow
            };

            companies.Add(newCompany);
            await _dataStorage.SaveCompaniesAsync(companies);

            _logger.LogInformation("Added new company: {CompanyName} (ID: {CompanyId})", newCompany.Name, newCompany.Id);

            return Ok(new
            {
                success = true,
                message = $"Company '{newCompany.Name}' added successfully.",
                company = new
                {
                    id = newCompany.Id,
                    name = newCompany.Name,
                    placeId = newCompany.PlaceId,
                    googleMapsUrl = newCompany.GoogleMapsUrl,
                    isActive = newCompany.IsActive,
                    lastUpdated = newCompany.LastUpdated
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding company");
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while adding the company.",
                error = ex.Message
            });
        }
    }

    [HttpGet("companies/{companyId}/rating")]
    public async Task<IActionResult> GetCompanyRating(string companyId)
    {
        try
        {
            var companies = await _dataStorage.GetCompaniesAsync();
            var company = companies.FirstOrDefault(c => c.Id == companyId);
            
            if (company == null)
            {
                return NotFound(new { success = false, message = $"Company with ID '{companyId}' not found." });
            }

            var reviewData = await _reviewService.GetReviewsAsync(company);
            
            if (reviewData == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        companyId = company.Id,
                        companyName = company.Name,
                        rating = 0.0,
                        totalReviews = 0,
                        stars = GenerateStarDisplay(0.0),
                        lastUpdated = (DateTime?)null
                    }
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    companyId = reviewData.CompanyId,
                    companyName = reviewData.CompanyName,
                    rating = reviewData.Rating,
                    totalReviews = reviewData.UserRatingsTotalCount,
                    stars = GenerateStarDisplay(reviewData.Rating),
                    lastUpdated = reviewData.LastUpdated
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company rating for {CompanyId}", companyId);
            
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while retrieving company rating.",
                error = ex.Message
            });
        }
    }

    private static string GenerateCompanyId(string name)
    {
        return name.ToLowerInvariant()
                   .Replace(" ", "-")
                   .Replace("å", "a")
                   .Replace("ä", "a")
                   .Replace("ö", "o")
                   .Replace(".", "")
                   .Replace(",", "")
                   .Replace("&", "and")
                   .Trim('-');
    }

    private static string GenerateStarDisplay(double rating)
    {
        var fullStars = (int)Math.Floor(rating);
        var hasHalfStar = rating - fullStars >= 0.5;
        var emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

        var stars = new string('★', fullStars);
        if (hasHalfStar) stars += "☆";
        stars += new string('☆', emptyStars);

        return stars;
    }
    
    [HttpGet("test-api")]
    public async Task<IActionResult> TestGooglePlacesApi()
    {
        try
        {
            _logger.LogInformation("Testing Google Places API connection");
            
            var companies = await _dataStorage.GetCompaniesAsync();
            var activeCompany = companies.FirstOrDefault(c => c.IsActive);
            
            if (activeCompany == null)
            {
                return BadRequest(new { success = false, message = "No active companies found to test with." });
            }
            
            _logger.LogInformation("Testing API with company: {CompanyName}, PlaceId: {PlaceId}", 
                activeCompany.Name, activeCompany.PlaceId);
            
            var reviewData = await _reviewService.GetReviewsAsync(activeCompany);
            
            if (reviewData == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "API call returned null. Check logs for detailed error information.",
                    company = new { activeCompany.Name, activeCompany.PlaceId }
                });
            }
            
            return Ok(new
            {
                success = true,
                message = "API test successful!",
                data = new
                {
                    companyName = reviewData.CompanyName,
                    rating = reviewData.Rating,
                    totalReviews = reviewData.UserRatingsTotalCount,
                    reviewsRetrieved = reviewData.Reviews.Count,
                    lastUpdated = reviewData.LastUpdated,
                    sampleReview = reviewData.Reviews.FirstOrDefault() != null 
                        ? new
                        {
                            author = reviewData.Reviews.First().AuthorName,
                            rating = reviewData.Reviews.First().Rating,
                            text = reviewData.Reviews.First().Text?.Substring(0, Math.Min(200, reviewData.Reviews.First().Text?.Length ?? 0)),
                            date = reviewData.Reviews.First().Time
                        }
                        : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Google Places API");
            
            return StatusCode(500, new
            {
                success = false,
                message = "Error testing API",
                error = ex.Message
            });
        }
    }
}

public class AddCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? PlaceId { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public bool IsActive { get; set; } = true;
}