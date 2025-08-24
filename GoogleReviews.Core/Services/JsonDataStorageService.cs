using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GoogleReviews.Core.Services;

public class JsonDataStorageService : IDataStorageService
{
    private readonly ILogger<JsonDataStorageService> _logger;
    private readonly string _dataDirectory;
    private readonly string _companiesFile;
    private readonly string _reviewsDirectory;

    public JsonDataStorageService(ILogger<JsonDataStorageService> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        _companiesFile = Path.Combine(_dataDirectory, "companies.json");
        _reviewsDirectory = Path.Combine(_dataDirectory, "Reviews");
        
        EnsureDirectoriesExist();
    }

    public async Task<List<Company>> GetCompaniesAsync()
    {
        try
        {
            if (!File.Exists(_companiesFile))
            {
                _logger.LogInformation("Companies file not found, creating default");
                var defaultCompanies = CreateDefaultCompanies();
                await SaveCompaniesAsync(defaultCompanies);
                return defaultCompanies;
            }

            var json = await File.ReadAllTextAsync(_companiesFile);
            var companies = JsonConvert.DeserializeObject<List<Company>>(json) ?? new List<Company>();
            
            _logger.LogInformation("Loaded {Count} companies from file", companies.Count);
            return companies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading companies from file");
            return new List<Company>();
        }
    }

    public async Task SaveCompaniesAsync(List<Company> companies)
    {
        try
        {
            var json = JsonConvert.SerializeObject(companies, Formatting.Indented);
            await File.WriteAllTextAsync(_companiesFile, json);
            _logger.LogInformation("Saved {Count} companies to file", companies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving companies to file");
        }
    }

    public async Task<CompanyReviewData?> GetHistoricalDataAsync(string companyId)
    {
        try
        {
            var filePath = Path.Combine(_reviewsDirectory, $"{companyId}.json");
            
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("No historical data found for company {CompanyId}", companyId);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonConvert.DeserializeObject<CompanyReviewData>(json);
            
            _logger.LogDebug("Loaded historical data for company {CompanyId}", companyId);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading historical data for company {CompanyId}", companyId);
            return null;
        }
    }

    public async Task SaveHistoricalDataAsync(CompanyReviewData data)
    {
        try
        {
            var filePath = Path.Combine(_reviewsDirectory, $"{data.CompanyId}.json");
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved historical data for company {CompanyId}", data.CompanyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving historical data for company {CompanyId}", data.CompanyId);
        }
    }

    public async Task<List<CompanyReviewData>> GetAllHistoricalDataAsync()
    {
        try
        {
            var allData = new List<CompanyReviewData>();
            var files = Directory.GetFiles(_reviewsDirectory, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var data = JsonConvert.DeserializeObject<CompanyReviewData>(json);
                    if (data != null)
                    {
                        allData.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading file {FileName}", Path.GetFileName(file));
                }
            }
            
            _logger.LogInformation("Loaded historical data for {Count} companies", allData.Count);
            return allData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all historical data");
            return new List<CompanyReviewData>();
        }
    }

    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Created data directory: {Directory}", _dataDirectory);
        }
        
        if (!Directory.Exists(_reviewsDirectory))
        {
            Directory.CreateDirectory(_reviewsDirectory);
            _logger.LogInformation("Created reviews directory: {Directory}", _reviewsDirectory);
        }
    }

    private static List<Company> CreateDefaultCompanies()
    {
        return new List<Company>
        {
            new()
            {
                Id = "ica-maxi-kalmar",
                Name = "ICA Maxi Kalmar",
                PlaceId = "", // User needs to provide actual PlaceIds
                IsActive = true
            },
            new()
            {
                Id = "sample-restaurant",
                Name = "Sample Restaurant",
                PlaceId = "", // User needs to provide actual PlaceIds
                IsActive = true
            }
        };
    }
}