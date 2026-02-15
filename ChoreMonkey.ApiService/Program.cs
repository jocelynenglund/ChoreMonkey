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

var app = builder.Build();

// Configure the HTTP request pipeline. 
app.UseExceptionHandler();

// Enable CORS
app.UseCors("ChoreMonkeyCors");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapChoreMonkeyEndpoints();
app.MapChoreMonkeyHub();

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
