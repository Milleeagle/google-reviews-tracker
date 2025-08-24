using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PuppeteerSharp;
using System.Text.RegularExpressions;

namespace GoogleReviews.Core.Services;

public class GoogleMapsScrapeService : IReviewService, IDisposable
{
    private readonly ILogger<GoogleMapsScrapeService> _logger;
    private readonly IConfiguration _configuration;
    private IBrowser? _browser;
    private readonly bool _headless;
    private readonly int _delayMs;

    public GoogleMapsScrapeService(IConfiguration configuration, ILogger<GoogleMapsScrapeService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _headless = _configuration["Scraping:Headless"] == "false" ? false : true;
        _delayMs = int.TryParse(_configuration["Scraping:DelayMs"], out var delay) ? delay : 2000;
    }

    public async Task<CompanyReviewData?> GetReviewsAsync(Company company)
    {
        try
        {
            if (string.IsNullOrEmpty(company.GoogleMapsUrl) && string.IsNullOrEmpty(company.PlaceId))
            {
                _logger.LogWarning("No Google Maps URL or PlaceId provided for company {CompanyName}", company.Name);
                return null;
            }

            // Build the URL - either from GoogleMapsUrl or construct from PlaceId
            var url = !string.IsNullOrEmpty(company.GoogleMapsUrl) 
                ? company.GoogleMapsUrl
                : $"https://maps.google.com/maps?place_id={company.PlaceId}&hl=en";

            _logger.LogInformation("Scraping reviews for {CompanyName} from {Url}", company.Name, url);

            await InitializeBrowserAsync();
            if (_browser == null)
            {
                _logger.LogError("Failed to initialize browser for company {CompanyName}", company.Name);
                return null;
            }

            var page = await _browser.NewPageAsync();
            
            try
            {
                // Set user agent to appear more like a real browser
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                
                // Navigate to the page
                await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 } });
                
                // Handle consent page if it appears
                await HandleConsentPageAsync(page);
                
                // Wait for the page to load
                await Task.Delay(_delayMs);

                // Extract business name
                var businessName = await ExtractBusinessNameAsync(page, company.Name);
                
                // Extract rating
                var rating = await ExtractRatingAsync(page);
                
                // Extract total review count
                var totalReviews = await ExtractTotalReviewCountAsync(page);
                
                // Try to load more reviews by clicking "Show more reviews" or scrolling
                await TryLoadMoreReviewsAsync(page);
                
                // Extract individual reviews
                var reviews = await ExtractReviewsAsync(page, company.Id);

                _logger.LogInformation("Scraped {ReviewCount} reviews for {CompanyName}, Rating: {Rating}, Total: {Total}", 
                    reviews.Count, company.Name, rating, totalReviews);

                return new CompanyReviewData
                {
                    CompanyId = company.Id,
                    CompanyName = businessName,
                    Rating = rating,
                    UserRatingsTotalCount = totalReviews,
                    Reviews = reviews,
                    LastUpdated = DateTime.UtcNow
                };
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping reviews for company {CompanyName}", company.Name);
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

                // Simple file-based storage for comparison (same as API version)
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

    private async Task InitializeBrowserAsync()
    {
        if (_browser != null) return;

        try
        {
            // Download browser if needed
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            
            // Launch browser
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = _headless,
                Args = new[] { 
                    "--no-sandbox", 
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-accelerated-2d-canvas",
                    "--no-first-run",
                    "--no-zygote",
                    "--disable-gpu"
                }
            });

            _logger.LogInformation("Browser initialized successfully (Headless: {Headless})", _headless);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize browser");
            _browser = null;
        }
    }

    private async Task<string> ExtractBusinessNameAsync(IPage page, string fallbackName)
    {
        try
        {
            // Try different selectors for business name
            var selectors = new[]
            {
                "h1[data-attrid='title']",
                "h1.x3AX1-LfntMc-header-title-title",
                "[data-attrid='title'] span",
                "h1"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        var text = await element.GetPropertyAsync("textContent");
                        var name = await text.JsonValueAsync<string>();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return name.Trim();
                        }
                    }
                }
                catch
                {
                    // Continue to next selector
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract business name, using fallback");
        }

        return fallbackName;
    }

    private async Task<double> ExtractRatingAsync(IPage page)
    {
        try
        {
            // Try different selectors for rating
            var selectors = new[]
            {
                "[data-attrid='kc:/collection/knowledge_panels/local_reviewable:star_score'] span",
                ".ceNzKf",
                "[jsaction*='rating'] span"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        var text = await element.GetPropertyAsync("textContent");
                        var ratingText = await text.JsonValueAsync<string>();
                        
                        if (!string.IsNullOrWhiteSpace(ratingText))
                        {
                            // Extract number from text like "4.2" or "4,2"
                            var match = Regex.Match(ratingText, @"([0-5])[,.]?(\d)?");
                            if (match.Success && double.TryParse(match.Value.Replace(',', '.'), out var rating))
                            {
                                return rating;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue to next selector
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract rating");
        }

        return 0.0;
    }

    private async Task<int> ExtractTotalReviewCountAsync(IPage page)
    {
        try
        {
            // Look for patterns like "(123 reviews)" or "123 recensioner"
            var textContent = await page.GetContentAsync();
            
            var patterns = new[]
            {
                @"\((\d+)\s+reviews?\)",
                @"\((\d+)\s+recensioner?\)",
                @"(\d+)\s+reviews?",
                @"(\d+)\s+recensioner?",
                @"Based on (\d+) review"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(textContent, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[1].Value, out var count))
                    {
                        return count;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract total review count");
        }

        return 0;
    }

    private async Task TryLoadMoreReviewsAsync(IPage page)
    {
        try
        {
            // Try to click "Show more reviews" or similar buttons
            var moreButtonSelectors = new[]
            {
                "[data-value='Sort']",
                "button[data-value='Sort']",
                ".review-more-link"
            };

            foreach (var selector in moreButtonSelectors)
            {
                try
                {
                    var button = await page.QuerySelectorAsync(selector);
                    if (button != null)
                    {
                        await button.ClickAsync();
                        await Task.Delay(2000); // Wait for more reviews to load
                        break;
                    }
                }
                catch
                {
                    // Continue to next selector
                }
            }

            // Try scrolling to load more content
            await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load more reviews");
        }
    }

    private async Task<List<Review>> ExtractReviewsAsync(IPage page, string companyId)
    {
        var reviews = new List<Review>();

        try
        {
            // Try different selectors for review containers
            var containerSelectors = new[]
            {
                ".jftiEf", // Google Maps review container
                ".ODSEW-ShBeI-content",
                "[data-review-id]",
                ".review-item"
            };

            IElementHandle[]? reviewElements = null;

            foreach (var containerSelector in containerSelectors)
            {
                try
                {
                    reviewElements = await page.QuerySelectorAllAsync(containerSelector);
                    if (reviewElements?.Length > 0)
                    {
                        _logger.LogDebug("Found {Count} review elements using selector {Selector}", 
                            reviewElements.Length, containerSelector);
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (reviewElements == null || reviewElements.Length == 0)
            {
                _logger.LogWarning("No review elements found");
                return reviews;
            }

            for (int i = 0; i < reviewElements.Length; i++)
            {
                try
                {
                    var reviewElement = reviewElements[i];
                    
                    // Extract author name
                    var authorName = await ExtractTextFromElement(reviewElement, new[]
                    {
                        ".d4r55",
                        ".a-profile-name",
                        "[data-attrid='title']"
                    }) ?? "Anonymous";

                    // Extract rating (star count)
                    var rating = await ExtractRatingFromElement(reviewElement);

                    // Extract review text
                    var reviewText = await ExtractTextFromElement(reviewElement, new[]
                    {
                        ".wiI7pd",
                        ".MyEned",
                        ".review-text",
                        "[data-attrid='review_text']"
                    });

                    // Extract relative time (like "2 months ago")
                    var timeText = await ExtractTextFromElement(reviewElement, new[]
                    {
                        ".rsqaWe",
                        ".review-date",
                        ".review-time"
                    });

                    var review = new Review
                    {
                        Id = $"{companyId}_scraped_{i}_{DateTime.UtcNow.Ticks}",
                        CompanyId = companyId,
                        AuthorName = authorName,
                        Rating = rating,
                        Text = reviewText,
                        Time = ParseRelativeTime(timeText),
                        AuthorUrl = null,
                        ProfilePhotoUrl = null
                    };

                    reviews.Add(review);
                    
                    if (reviews.Count >= 10) break; // Limit to prevent too much data
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error extracting review {Index}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting reviews");
        }

        _logger.LogInformation("Successfully extracted {Count} reviews", reviews.Count);
        return reviews;
    }

    private async Task<string?> ExtractTextFromElement(IElementHandle element, string[] selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var childElement = await element.QuerySelectorAsync(selector);
                if (childElement != null)
                {
                    var textProperty = await childElement.GetPropertyAsync("textContent");
                    var text = await textProperty.JsonValueAsync<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch
            {
                continue;
            }
        }
        return null;
    }

    private async Task<int> ExtractRatingFromElement(IElementHandle element)
    {
        try
        {
            // Look for star ratings
            var starSelectors = new[]
            {
                "[aria-label*='star']",
                ".kvMYJc",
                "[role='img'][aria-label*='star']"
            };

            foreach (var selector in starSelectors)
            {
                try
                {
                    var starElement = await element.QuerySelectorAsync(selector);
                    if (starElement != null)
                    {
                        var ariaLabel = await starElement.GetPropertyAsync("ariaLabel");
                        var label = await ariaLabel.JsonValueAsync<string>();
                        
                        if (!string.IsNullOrEmpty(label))
                        {
                            // Extract number from "5 stars" or "3 stjärnor"
                            var match = Regex.Match(label, @"(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var stars))
                            {
                                return stars;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract rating from review element");
        }

        return 0;
    }

    private DateTime ParseRelativeTime(string? timeText)
    {
        if (string.IsNullOrWhiteSpace(timeText))
            return DateTime.UtcNow;

        try
        {
            var now = DateTime.UtcNow;
            timeText = timeText.ToLowerInvariant();

            // Parse patterns like "2 months ago", "3 weeks ago", "1 year ago"
            if (timeText.Contains("month"))
            {
                var match = Regex.Match(timeText, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var months))
                {
                    return now.AddMonths(-months);
                }
            }
            else if (timeText.Contains("week"))
            {
                var match = Regex.Match(timeText, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var weeks))
                {
                    return now.AddDays(-weeks * 7);
                }
            }
            else if (timeText.Contains("day"))
            {
                var match = Regex.Match(timeText, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
                {
                    return now.AddDays(-days);
                }
            }
            else if (timeText.Contains("year"))
            {
                var match = Regex.Match(timeText, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var years))
                {
                    return now.AddYears(-years);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse relative time: {TimeText}", timeText);
        }

        return DateTime.UtcNow;
    }

    private async Task HandleConsentPageAsync(IPage page)
    {
        try
        {
            // Check if we're on a consent page
            var title = await page.GetTitleAsync();
            if (title.Contains("Before you continue") || title.Contains("Innan du fortsätter") || 
                title.Contains("consent") || title.Contains("cookie"))
            {
                _logger.LogInformation("Detected consent page, attempting to handle it");

                // Try to find and click accept buttons
                var acceptSelectors = new[]
                {
                    "[data-testid='accept-button']",
                    "button[data-value='accept']",
                    "button[data-value='I agree']",
                    "button:contains('Accept all')",
                    "button:contains('Accept')",
                    "button:contains('I agree')",
                    "#L2AGLb", // Google's "I agree" button ID
                    ".QS5gu", // Google's accept button class
                    "[jsname='V68bde']" // Another Google consent button
                };

                foreach (var selector in acceptSelectors)
                {
                    try
                    {
                        var button = await page.QuerySelectorAsync(selector);
                        if (button != null)
                        {
                            _logger.LogInformation("Clicking consent button with selector: {Selector}", selector);
                            await button.ClickAsync();
                            await Task.Delay(2000); // Wait for navigation
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Failed to click consent button {Selector}: {Error}", selector, ex.Message);
                    }
                }

                // Wait for potential navigation after consent
                await Task.Delay(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling consent page");
        }
    }

    private static ReviewChange? CompareReviewData(CompanyReviewData historical, CompanyReviewData current)
    {
        var hasRatingChanged = Math.Abs(historical.Rating - current.Rating) > 0.01;
        var hasTotalCountChanged = historical.UserRatingsTotalCount != current.UserRatingsTotalCount;

        if (!hasRatingChanged && !hasTotalCountChanged) return null;

        var newReviews = current.Reviews
            .Where(cr => !historical.Reviews.Any(hr => hr.AuthorName == cr.AuthorName && hr.Rating == cr.Rating && hr.Text == cr.Text))
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

    public void Dispose()
    {
        try
        {
            _browser?.CloseAsync().GetAwaiter().GetResult();
            _browser?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing browser");
        }
    }
}