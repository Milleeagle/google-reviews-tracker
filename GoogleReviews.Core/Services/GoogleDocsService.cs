using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace GoogleReviews.Core.Services;

public class GoogleDocsService : IGoogleDocsService
{
    private readonly ILogger<GoogleDocsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _documentId;

    public GoogleDocsService(IConfiguration configuration, ILogger<GoogleDocsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _documentId = configuration["GoogleDocs:DocumentId"] ?? throw new ArgumentException("Google Docs Document ID not configured");
    }

    public async Task ReportChangesAsync(List<ReviewChange> changes)
    {
        if (!changes.Any())
        {
            _logger.LogInformation("No changes to report to Google Docs");
            return;
        }

        try
        {
            var service = await CreateDocsServiceAsync();
            var content = GenerateReportContent(changes);
            
            await AppendToDocumentAsync(service, content);
            
            _logger.LogInformation("Successfully reported {Count} changes to Google Docs", changes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting changes to Google Docs");
        }
    }

    private async Task<DocsService> CreateDocsServiceAsync()
    {
        var credentialsPath = _configuration["GoogleDocs:ServiceAccountKeyPath"];
        GoogleCredential credential;
        
        if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
        {
            // Use Service Account credentials
            credential = GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(DocsService.Scope.Documents);
        }
        else
        {
            // Use default credentials (for local development)
            credential = await GoogleCredential.GetApplicationDefaultAsync();
            credential = credential.CreateScoped(DocsService.Scope.Documents);
        }

        return new DocsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Google Reviews Tracker"
        });
    }

    private async Task AppendToDocumentAsync(DocsService service, string content)
    {
        try
        {
            // Get document to find the end index
            var document = await service.Documents.Get(_documentId).ExecuteAsync();
            var endIndex = document.Body.Content.Last().EndIndex - 1;

            // Create insert request
            var insertRequest = new BatchUpdateDocumentRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        InsertText = new InsertTextRequest
                        {
                            Location = new Location { Index = endIndex },
                            Text = content
                        }
                    }
                }
            };

            await service.Documents.BatchUpdate(insertRequest, _documentId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending content to Google Doc");
            throw;
        }
    }

    private string GenerateReportContent(List<ReviewChange> changes)
    {
        var report = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        report.AppendLine($"\n--- Google Reviews Report - {timestamp} ---\n");
        
        foreach (var change in changes)
        {
            report.AppendLine($"🏢 Företag: {change.CompanyName}");
            
            if (change.ChangeType == ChangeType.RatingChanged || change.ChangeType == ChangeType.Both)
            {
                report.AppendLine($"⭐ Tidigare betyg: {change.PreviousRating:F1}");
                report.AppendLine($"⭐ Nuvarande betyg: {change.CurrentRating:F1}");
                
                var ratingChange = change.CurrentRating - (change.PreviousRating ?? 0);
                var changeIcon = ratingChange > 0 ? "📈" : "📉";
                report.AppendLine($"{changeIcon} Förändring: {ratingChange:+0.0;-0.0}");
            }
            
            if (change.ChangeType == ChangeType.NewReviews || change.ChangeType == ChangeType.Both)
            {
                report.AppendLine($"📊 Totalt antal recensioner: {change.PreviousTotalReviews} → {change.CurrentTotalReviews}");
                report.AppendLine($"🆕 Nya recensioner: {change.NewReviews.Count}");
                
                if (change.NewReviews.Any())
                {
                    report.AppendLine("\nSenaste nya recensioner:");
                    foreach (var review in change.NewReviews.Take(3))
                    {
                        var stars = new string('⭐', review.Rating);
                        report.AppendLine($"  • {stars} av {review.AuthorName} ({review.Time:yyyy-MM-dd})");
                        if (!string.IsNullOrEmpty(review.Text) && review.Text.Length > 100)
                        {
                            report.AppendLine($"    \"{review.Text[..97]}...\"");
                        }
                        else if (!string.IsNullOrEmpty(review.Text))
                        {
                            report.AppendLine($"    \"{review.Text}\"");
                        }
                    }
                    
                    if (change.NewReviews.Count > 3)
                    {
                        report.AppendLine($"  ... och {change.NewReviews.Count - 3} fler nya recensioner");
                    }
                }
            }
            
            report.AppendLine($"🕐 Upptäckt: {change.DetectedAt:yyyy-MM-dd HH:mm}");
            report.AppendLine(new string('-', 50));
            report.AppendLine();
        }
        
        return report.ToString();
    }
}