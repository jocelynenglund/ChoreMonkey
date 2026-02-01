var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.ChoreMonkey_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var webApp = builder.AddNpmApp("web", "../nestle-together", "dev")
    .WithEnvironment("VITE_API_URL", apiService.GetEndpoint("http"));

builder.Build().Run();
