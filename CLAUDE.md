# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9.0 Aspire application that implements a Google Reviews Tracker system. The project monitors Google Reviews for businesses, detects changes, and reports them to Google Docs automatically. It uses Microsoft's Aspire framework for orchestrating the distributed application components.

## Project Structure

```
google_reviews/
├── google_reviews/                        # Aspire AppHost (orchestrator)
│   ├── google_reviews.sln                # Solution file
│   └── google_reviews/
│       ├── AppHost.cs                     # Main Aspire application entry point
│       ├── google_reviews.csproj          # AppHost project file
│       └── Properties/launchSettings.json # Launch profiles and settings
├── GoogleReviews.Api/                     # Web API application
│   ├── Controllers/ReviewsController.cs   # API endpoints for manual triggers
│   ├── BackgroundServices/               
│   │   └── ReviewMonitoringService.cs    # Weekly background service
│   ├── Program.cs                        # API startup with embedded dashboard
│   └── appsettings.json                  # API configuration
├── GoogleReviews.Core/                    # Business logic library
│   ├── Models/                           # Data models (Company, Review, etc.)
│   ├── Services/                         # Core services
│   │   ├── GooglePlacesService.cs        # Google Places API integration
│   │   ├── GoogleDocsService.cs          # Google Docs reporting
│   │   ├── GoogleMapsScrapeService.cs    # Web scraping alternative
│   │   ├── ReviewServiceFactory.cs       # Dynamic service selection
│   │   └── JsonDataStorageService.cs     # JSON-based data storage
│   └── Interfaces/                       # Service interfaces
└── Data/                                 # Data storage
    ├── companies.json                    # Company configuration
    └── Reviews/                          # Historical review data (auto-created)
```

## Development Commands

### Building and Running
```bash
# Build the entire solution
dotnet build

# Run via Aspire AppHost (recommended - orchestrates all services)
cd google_reviews/google_reviews
dotnet run

# Run API directly (for development/testing)
cd GoogleReviews.Api
dotnet run

# Run in specific configuration
dotnet run --configuration Release
```

### Testing and Development
```bash
# Run all tests (when tests are added)
dotnet test

# Restore dependencies
dotnet restore

# Add package to specific project
dotnet add GoogleReviews.Api package [PackageName]
dotnet add GoogleReviews.Core package [PackageName]

# Run API with hot reload
cd GoogleReviews.Api
dotnet watch run
```

## Application Architecture

This is a multi-layered Aspire application with clear separation of concerns:

### Components:
- **AppHost** (`google_reviews/google_reviews`): Aspire orchestrator that manages and monitors the API service
- **GoogleReviews.Api**: ASP.NET Core Web API with built-in dashboard and background services
- **GoogleReviews.Core**: Business logic layer containing models, services, and interfaces

### Key Services:
- **GooglePlacesService**: Fetches reviews from Google Places API
- **GoogleMapsScrapeService**: Alternative web scraping service (free option)
- **DynamicReviewService**: Switches between API and scraping based on configuration
- **GoogleDocsService**: Reports changes to Google Docs using service account authentication
- **JsonDataStorageService**: Manages company data and historical review storage
- **ReviewMonitoringService**: Background service that runs weekly to detect changes

### Data Flow:
1. Background service reads companies from `Data/companies.json`
2. Fetches current reviews via Google Places API or web scraping (based on configuration)
3. Compares with historical data stored in `Data/Reviews/`
4. Detects rating changes and new reviews
5. Reports changes to Google Docs
6. Updates historical data for next comparison

### Review Service Configuration:
The system supports two modes for fetching reviews:
- **API Mode**: Uses Google Places API (requires API key, costs money, reliable)
- **Scraping Mode**: Uses web scraping (free, may be less reliable)

Configure via `appsettings.json`:
```json
{
  "ReviewService": {
    "Type": "API",  // or "Scraping"
  },
  "Scraping": {
    "Headless": false,    // Set to true for production
    "DelayMs": 3000      // Delay for page loading
  }
}
```

### Configuration Requirements:
- **Google Places API Key**: Required for API mode
- **Google Service Account**: Required for Google Docs integration
- **Google Docs Document ID**: Target document for reports
- **User Secrets**: ID `f5f31edc-8e45-4cc9-980c-0b5731c7f49b`

## API Endpoints

When running the API (via Aspire or directly):
- **Dashboard**: `GET /` - Web dashboard for monitoring and manual triggers
- **Manual Check**: `POST /api/reviews/check` - Trigger review check manually
- **System Status**: `GET /api/reviews/status` - Get current system status
- **Companies**: `GET /api/reviews/companies` - List all configured companies
- **Add Company**: `POST /api/reviews/companies` - Add new company
- **Company Reviews**: `GET /api/reviews/companies/{id}/reviews` - Get reviews for specific company
- **Company Rating**: `GET /api/reviews/companies/{id}/rating` - Get current rating for company

## Configuration Setup

### Required Configuration Files:
1. **GoogleReviews.Api/appsettings.json**: 
   - Set `GoogleApi.ApiKey` to your Google Places API key (if using API mode)
   - Set `GoogleDocs.DocumentId` to your target Google Docs document ID
   - Set `GoogleDocs.ServiceAccountKeyPath` to your service account JSON file path
   - Set `ReviewService.Type` to "API" or "Scraping"

2. **Data/companies.json**:
   - Add companies with their Google Places `PlaceId` (for API mode) or `GoogleMapsUrl` (for scraping)
   - Set `IsActive: true` for companies to monitor

### Google Cloud Setup Required:
1. Enable Google Places API and get API key (if using API mode)
2. Enable Google Docs API and create service account
3. Share target Google Docs document with service account email
4. Download service account JSON key file

## Key Features

- **Automated Monitoring**: Weekly background checks via `ReviewMonitoringService`
- **Dual Review Sources**: Support for both Google Places API and web scraping
- **Change Detection**: Compares current reviews with historical data
- **Swedish Reports**: Generates reports in Swedish for Google Docs
- **Web Dashboard**: Built-in dashboard for monitoring and manual operations with company management
- **Aspire Integration**: Full observability and service orchestration
- **JSON Storage**: Simple file-based storage for companies and historical data