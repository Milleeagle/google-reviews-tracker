using GoogleReviews.Api.BackgroundServices;
using GoogleReviews.Core.Interfaces;
using GoogleReviews.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Handle service account in production
if (builder.Environment.IsProduction())
{
    var serviceAccountBase64 = builder.Configuration["GoogleDocs:ServiceAccountBase64"];
    if (!string.IsNullOrEmpty(serviceAccountBase64))
    {
        var serviceAccountJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(serviceAccountBase64));
        var tempPath = "/app/service-account.json";
        await File.WriteAllTextAsync(tempPath, serviceAccountJson);
    }
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient
builder.Services.AddHttpClient();

// Register both review services
builder.Services.AddScoped<GooglePlacesService>();
builder.Services.AddScoped<GoogleMapsScrapeService>();

// Register the dynamic review service that switches based on configuration
builder.Services.AddScoped<IReviewService, DynamicReviewService>();

// Register other services
builder.Services.AddScoped<IDataStorageService, JsonDataStorageService>();
builder.Services.AddScoped<IGoogleDocsService, GoogleDocsService>();

// Register background service
builder.Services.AddHostedService<ReviewMonitoringService>();

// Add CORS for web dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

// Serve static files for dashboard
app.UseStaticFiles();

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Add a simple dashboard route
app.MapGet("/", () => Results.Content(GetDashboardHtml(), "text/html"));

app.MapControllers();

app.Run();

static string GetDashboardHtml()
{
    return """
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Google Reviews Tracker</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }
            .container { max-width: 800px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
            h1 { color: #333; text-align: center; margin-bottom: 30px; }
            .card { background: #f8f9fa; padding: 20px; margin: 15px 0; border-radius: 8px; border-left: 4px solid #007bff; }
            .button { background: #007bff; color: white; padding: 12px 24px; border: none; border-radius: 5px; cursor: pointer; margin: 10px 5px; font-size: 16px; }
            .button:hover { background: #0056b3; }
            .button:disabled { background: #6c757d; cursor: not-allowed; }
            .status { margin: 20px 0; padding: 15px; background: #e9ecef; border-radius: 5px; }
            .loading { color: #007bff; font-style: italic; }
            .success { color: #28a745; }
            .error { color: #dc3545; }
            .companies { margin: 20px 0; }
            .company-item { padding: 15px; margin: 10px 0; background: white; border: 1px solid #ddd; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
            .stars { font-size: 18px; color: #ffd700; margin: 5px 0; }
            .rating-number { font-weight: bold; color: #333; }
            .form-card { background: #f8f9fa; padding: 20px; margin: 15px 0; border-radius: 8px; border-left: 4px solid #28a745; }
            .form-row { display: flex; flex-wrap: wrap; gap: 10px; margin: 10px 0; }
            .form-group { flex: 1; min-width: 200px; }
            .form-group label { display: block; margin-bottom: 5px; font-weight: bold; }
            .form-group input, .form-group select { width: 100%; padding: 8px; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }
            .checkbox-group { display: flex; align-items: center; gap: 8px; }
            .checkbox-group input[type="checkbox"] { width: auto; }
        </style>
    </head>
    <body>
        <div class="container">
            <h1>🔍 Google Reviews Tracker</h1>
            
            <div class="card">
                <h2>📊 Dashboard</h2>
                <div id="status" class="status">Loading status...</div>
                
                <button id="checkBtn" class="button" onclick="triggerCheck()">
                    🔄 Trigger Manual Check
                </button>
                
                <button id="refreshBtn" class="button" onclick="loadStatus()">
                    📈 Refresh Status
                </button>
            </div>
            
            <div class="form-card">
                <h2>➕ Add New Company</h2>
                <form id="addCompanyForm">
                    <div class="form-row">
                        <div class="form-group">
                            <label for="companyName">Company Name *</label>
                            <input type="text" id="companyName" name="name" required placeholder="e.g. ICA Maxi Stockholm">
                        </div>
                        <div class="form-group">
                            <label for="placeId">Google Place ID</label>
                            <input type="text" id="placeId" name="placeId" placeholder="ChIJXXXXXXXXXXXXXXXXXXX (optional)">
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label for="googleMapsUrl">Google Maps URL</label>
                            <input type="url" id="googleMapsUrl" name="googleMapsUrl" placeholder="https://maps.google.com/... (optional)">
                        </div>
                        <div class="form-group checkbox-group">
                            <input type="checkbox" id="isActive" name="isActive" checked>
                            <label for="isActive">Active (monitor for changes)</label>
                        </div>
                    </div>
                    <button type="submit" class="button">➕ Add Company</button>
                </form>
            </div>
            
            <div class="card">
                <h2>🏢 Companies</h2>
                <div id="companies" class="companies">Loading companies...</div>
            </div>
        </div>

        <script>
            async function loadStatus() {
                document.getElementById('status').innerHTML = '<span class="loading">Loading...</span>';
                
                try {
                    const response = await fetch('/api/reviews/status');
                    const data = await response.json();
                    
                    if (data.success) {
                        const status = data.status;
                        document.getElementById('status').innerHTML = `
                            <strong>System Status:</strong><br>
                            📈 Total Companies: ${status.totalCompanies}<br>
                            ✅ Active Companies: ${status.activeCompanies}<br>
                            📊 Companies with Data: ${status.companiesWithData}<br>
                            🕐 Last Data Update: ${status.lastDataUpdate ? new Date(status.lastDataUpdate).toLocaleString() : 'Never'}<br>
                            ⏰ Server Time: ${new Date(status.serverTime).toLocaleString()}
                        `;
                    } else {
                        document.getElementById('status').innerHTML = `<span class="error">Error: ${data.message}</span>`;
                    }
                } catch (error) {
                    document.getElementById('status').innerHTML = `<span class="error">Error loading status: ${error.message}</span>`;
                }
            }
            
            async function loadCompanies() {
                document.getElementById('companies').innerHTML = '<span class="loading">Loading...</span>';
                
                try {
                    const response = await fetch('/api/reviews/companies');
                    const data = await response.json();
                    
                    if (data.success) {
                        let companiesHtml = '';
                        
                        for (const company of data.companies) {
                            // Get rating data for each company
                            let ratingHtml = '';
                            try {
                                const ratingResponse = await fetch(`/api/reviews/companies/${company.id}/rating`);
                                const ratingData = await ratingResponse.json();
                                
                                if (ratingData.success && ratingData.data.rating > 0) {
                                    ratingHtml = `
                                        <div class="stars">${ratingData.data.stars}</div>
                                        <div class="rating-number">${ratingData.data.rating.toFixed(1)}/5.0 (${ratingData.data.totalReviews} reviews)</div>
                                    `;
                                } else {
                                    ratingHtml = '<div class="rating-number">No rating data</div>';
                                }
                            } catch {
                                ratingHtml = '<div class="rating-number">Rating unavailable</div>';
                            }
                            
                            companiesHtml += `
                                <div class="company-item">
                                    <strong>${company.name}</strong> <small>(${company.id})</small><br>
                                    ${ratingHtml}
                                    <div style="margin-top: 8px;">
                                        Status: ${company.isActive ? '✅ Active' : '❌ Inactive'}<br>
                                        Place ID: ${company.placeId || 'Not set'}<br>
                                        Google Maps: ${company.googleMapsUrl ? '🔗 Set' : 'Not set'}<br>
                                        Last Updated: ${new Date(company.lastUpdated).toLocaleString()}
                                    </div>
                                </div>
                            `;
                        }
                        
                        document.getElementById('companies').innerHTML = companiesHtml || '<p>No companies found.</p>';
                    } else {
                        document.getElementById('companies').innerHTML = `<span class="error">Error: ${data.message}</span>`;
                    }
                } catch (error) {
                    document.getElementById('companies').innerHTML = `<span class="error">Error loading companies: ${error.message}</span>`;
                }
            }
            
            async function triggerCheck() {
                const checkBtn = document.getElementById('checkBtn');
                checkBtn.disabled = true;
                checkBtn.textContent = '⏳ Checking...';
                
                try {
                    const response = await fetch('/api/reviews/check', { method: 'POST' });
                    const data = await response.json();
                    
                    if (data.success) {
                        alert(`✅ Check completed!\n\n${data.message}\n\nChanges found: ${data.changes.length}`);
                        loadStatus(); // Refresh status after check
                    } else {
                        alert(`❌ Error: ${data.message}`);
                    }
                } catch (error) {
                    alert(`❌ Error during check: ${error.message}`);
                } finally {
                    checkBtn.disabled = false;
                    checkBtn.textContent = '🔄 Trigger Manual Check';
                }
            }
            
            async function addCompany() {
                const form = document.getElementById('addCompanyForm');
                const formData = new FormData(form);
                
                const companyData = {
                    name: formData.get('name'),
                    placeId: formData.get('placeId') || null,
                    googleMapsUrl: formData.get('googleMapsUrl') || null,
                    isActive: formData.has('isActive')
                };
                
                if (!companyData.name || companyData.name.trim() === '') {
                    alert('❌ Company name is required');
                    return;
                }
                
                try {
                    const response = await fetch('/api/reviews/companies', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify(companyData)
                    });
                    
                    const data = await response.json();
                    
                    if (data.success) {
                        alert(`✅ ${data.message}`);
                        form.reset();
                        document.getElementById('isActive').checked = true; // Reset checkbox to default
                        loadCompanies(); // Refresh the companies list
                        loadStatus(); // Refresh status to update counts
                    } else {
                        alert(`❌ Error: ${data.message}`);
                    }
                } catch (error) {
                    alert(`❌ Error adding company: ${error.message}`);
                }
            }
            
            // Load initial data
            window.onload = function() {
                loadStatus();
                loadCompanies();
                
                // Set up form submission
                document.getElementById('addCompanyForm').addEventListener('submit', function(e) {
                    e.preventDefault();
                    addCompany();
                });
            };
        </script>
    </body>
    </html>
    """;
}