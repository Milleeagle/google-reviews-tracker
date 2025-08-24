using GoogleReviews.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoogleReviews.Api.BackgroundServices;

public class ReviewMonitoringService : BackgroundService
{
    private readonly ILogger<ReviewMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval;

    public ReviewMonitoringService(
        ILogger<ReviewMonitoringService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Run once per week (7 days)
        _checkInterval = TimeSpan.FromDays(7);
        
        // For testing, uncomment this to run every 5 minutes:
        // _checkInterval = TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Review Monitoring Service started. Will check reviews every {Interval}.", _checkInterval);

        // Run immediately on startup, then wait for intervals
        await CheckReviewsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    await CheckReviewsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Review Monitoring Service is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Review Monitoring Service main loop");
                
                // Wait a bit before trying again if there's an error
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }

    private async Task CheckReviewsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var dataStorage = scope.ServiceProvider.GetRequiredService<IDataStorageService>();
        var googleDocs = scope.ServiceProvider.GetRequiredService<IGoogleDocsService>();

        try
        {
            _logger.LogInformation("Starting review check at {Timestamp}", DateTime.UtcNow);

            var companies = await dataStorage.GetCompaniesAsync();
            var activeCompanies = companies.Where(c => c.IsActive).ToList();
            
            _logger.LogInformation("Checking reviews for {Count} active companies", activeCompanies.Count);

            var changes = await reviewService.DetectChangesAsync(activeCompanies);

            if (changes.Any())
            {
                _logger.LogInformation("Found {Count} companies with changes", changes.Count);
                
                foreach (var change in changes)
                {
                    _logger.LogInformation("Company {CompanyName}: {ChangeType}", 
                        change.CompanyName, change.ChangeType);
                }

                await googleDocs.ReportChangesAsync(changes);
                _logger.LogInformation("Successfully reported changes to Google Docs");
            }
            else
            {
                _logger.LogInformation("No changes detected in any company reviews");
            }

            _logger.LogInformation("Review check completed successfully at {Timestamp}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during review check");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Review Monitoring Service is stopping...");
        await base.StopAsync(stoppingToken);
        _logger.LogInformation("Review Monitoring Service stopped.");
    }
}