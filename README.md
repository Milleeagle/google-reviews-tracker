# 🔍 Google Reviews Tracker

A modern .NET 9.0 Aspire application that automatically monitors Google Reviews for businesses and reports changes to Google Docs.

## ✨ Features

- 🏢 **Multi-company monitoring** - Track reviews for multiple businesses
- ⭐ **Dual data sources** - Google Places API + Web scraping fallback
- 🔄 **Change detection** - Automatically detects rating changes and new reviews
- 📝 **Google Docs integration** - Automated Swedish reports
- ⏰ **Background monitoring** - Weekly automated checks
- 🎛️ **Web dashboard** - Built-in management interface
- 🚀 **Modern stack** - .NET 9.0, Aspire, ASP.NET Core

## 🚀 Quick Start

### Prerequisites
- .NET 9.0 SDK
- Google Cloud Project with Places API enabled
- Google Service Account for Docs API

### Running Locally
```bash
# Clone the repository
git clone <your-repo-url>
cd google_reviews

# Build the solution
dotnet build

# Run via Aspire (recommended)
cd google_reviews/google_reviews
dotnet run

# Or run API directly
cd GoogleReviews.Api
dotnet run
```

### Configuration
Update `GoogleReviews.Api/appsettings.json`:
```json
{
  "GoogleApi": {
    "ApiKey": "YOUR_GOOGLE_PLACES_API_KEY"
  },
  "GoogleDocs": {
    "DocumentId": "YOUR_GOOGLE_DOCS_DOCUMENT_ID",
    "ServiceAccountKeyPath": "path/to/service-account.json"
  },
  "ReviewService": {
    "Type": "API"
  }
}
```

Add companies to `Data/companies.json`:
```json
[
  {
    "Id": "your-company-id",
    "Name": "Your Company Name",
    "PlaceId": "ChIJXXXXXXXXXXXXX",
    "IsActive": true
  }
]
```

## 🌐 API Endpoints

- `GET /` - Web dashboard
- `POST /api/reviews/check` - Trigger manual review check
- `GET /api/reviews/companies` - List all companies
- `GET /api/reviews/companies/{id}/reviews` - Get company reviews
- `GET /api/reviews/test-api` - Test API connection
- `GET /health` - Health check

## 🏗️ Architecture

- **GoogleReviews.Api** - Web API with dashboard and background services
- **GoogleReviews.Core** - Business logic and data models
- **AppHost** - Aspire orchestration

### Key Services
- `GooglePlacesService` - Google Places API integration
- `GoogleMapsScrapeService` - Web scraping fallback
- `GoogleDocsService` - Google Docs reporting
- `ReviewMonitoringService` - Background monitoring

## 🚀 Deployment

This application is configured for Railway deployment:

1. Push to GitHub
2. Connect Railway to your repository
3. Set environment variables in Railway dashboard
4. Deploy automatically

### Environment Variables
```bash
GoogleApi__ApiKey=your_api_key
GoogleDocs__DocumentId=your_doc_id
GoogleDocs__ServiceAccountBase64=base64_encoded_service_account
ASPNETCORE_ENVIRONMENT=Production
```

## 📝 License

MIT License - see LICENSE file for details.

## 🤝 Contributing

1. Fork the project
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request