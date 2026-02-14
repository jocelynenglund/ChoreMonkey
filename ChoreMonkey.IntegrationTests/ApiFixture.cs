using FileEventStore;
using FileEventStore.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChoreMonkey.IntegrationTests;

/// <summary>
/// Shared fixture that boots the API with an isolated temp EventStore.
/// Use IClassFixture&lt;ApiFixture&gt; for test classes.
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    private readonly string _tempDataPath;
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;
    public string DataPath => _tempDataPath;

    public ApiFixture()
    {
        _tempDataPath = Path.Combine(Path.GetTempPath(), $"choremonkey-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDataPath);
    }

    public ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Remove the existing EventStore registration
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IEventSessionFactory>();

                    // Re-register with our temp path
                    services.AddFileEventStore(_tempDataPath);
                });
            });

        Client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        // Clean up temp directory
        try
        {
            if (Directory.Exists(_tempDataPath))
            {
                Directory.Delete(_tempDataPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Resets the event store by clearing all data files.
    /// Call between tests if you need isolation within a class.
    /// </summary>
    public void ResetEventStore()
    {
        if (Directory.Exists(_tempDataPath))
        {
            foreach (var file in Directory.GetFiles(_tempDataPath, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
    }
}

/// <summary>
/// Collection definition for tests that share the same ApiFixture instance.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
}
