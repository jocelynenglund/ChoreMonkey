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
        // Development-friendly policy; tighten for production
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
