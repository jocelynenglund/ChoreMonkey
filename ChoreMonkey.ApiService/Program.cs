using System.Reflection;
using System.Threading.RateLimiting;
using ChoreMonkey.Core;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure CORS for the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChoreMonkeyCors", policy =>
    {
        policy.WithOrigins(
                "http://labs.itsybit.se",
                "https://labs.itsybit.se",
                "http://choremonkey.itsybit.se",
                "https://choremonkey.itsybit.se",
                "http://localhost:5173",
                "https://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddChoreMonkeyCore();

// Rate limiting for auth endpoints (PIN brute-force protection)
// Disabled in Development/Test to allow integration tests to run
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        // Auth endpoints: 5 requests per minute per IP
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        
        // General API: 100 requests per minute per IP
        options.AddPolicy("api", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline. 
app.UseExceptionHandler();

// Enable CORS
app.UseCors("ChoreMonkeyCors");

// Enable rate limiting (production only)
if (!app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapChoreMonkeyEndpoints();
app.MapChoreMonkeyHub();

// Version endpoint
app.MapGet("/api/version", () => {
    var assembly = Assembly.GetExecutingAssembly();
    var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    
    // Parse InformationalVersion which is set to BUILD_VERSION (e.g., "2026.02.15.42")
    // or fallback to "version+sha" format
    string version;
    string gitSha;
    
    if (infoVersion.Contains('+'))
    {
        // Format: "1.0.0+abc1234"
        gitSha = infoVersion.Split('+').Last();
        if (gitSha.Length > 7) gitSha = gitSha[..7];
        var buildTime = File.GetLastWriteTimeUtc(assembly.Location);
        version = $"{buildTime:yyyy.MM.dd}.{gitSha}";
    }
    else if (infoVersion.Split('.').Length >= 4)
    {
        // Format: "2026.02.15.42" - already human-readable
        version = infoVersion;
        gitSha = Environment.GetEnvironmentVariable("GIT_SHA") ?? "unknown";
        if (gitSha.Length > 7) gitSha = gitSha[..7];
    }
    else
    {
        version = infoVersion;
        gitSha = "unknown";
    }
    
    var buildTimeUtc = File.GetLastWriteTimeUtc(assembly.Location);
    
    return Results.Ok(new { 
        version,
        buildTime = buildTimeUtc.ToString("o"),
        gitSha,
        component = "api"
    });
});

// Debug endpoint to check data path
app.MapGet("/api/debug/config", () => {
    var dataPath = Environment.GetEnvironmentVariable("EVENTSTORE_PATH") 
        ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
    return Results.Ok(new { 
        dataPath,
        exists = Directory.Exists(dataPath),
        currentDirectory = Directory.GetCurrentDirectory()
    });
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
