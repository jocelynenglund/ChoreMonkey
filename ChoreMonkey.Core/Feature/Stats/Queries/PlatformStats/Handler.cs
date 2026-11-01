using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace ChoreMonkey.Core.Feature.Stats.Queries.PlatformStats;

public record PlatformStatsQuery;

public record HouseholdStatsDto(
    Guid HouseholdId,
    string Name,
    int MemberCount,
    int ChoreCount,
    DateTime CreatedAt);

public record PlatformStatsResponse(
    int TotalHouseholds,
    int TotalMembers,
    int TotalChores,
    List<HouseholdStatsDto> Households,
    string? DebugDataPath = null,
    bool? DebugDirectoryExists = null);

internal class Handler(IEventStore store, IConfiguration config)
{
    private string GetDataPath() => Environment.GetEnvironmentVariable("EVENTSTORE_PATH")
        ?? config["EventStore:Path"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "data");

    public async Task<PlatformStatsResponse> HandleAsync()
    {
        var dataPath = GetDataPath();

        // Scan data directory for household stream files
        var householdStreamIds = new List<string>();

        if (Directory.Exists(dataPath))
        {
            var files = Directory.GetFiles(dataPath, "household-*.jsonl");
            householdStreamIds = files
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();
        }

        // One history per household: its household stream, with its chore stream appended
        // (the chores are only read for a stream that has a household created event).
        var households = new List<IReadOnlyList<object>>();
        foreach (var streamId in householdStreamIds)
        {
            var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

            var created = history.OfType<HouseholdCreated>().FirstOrDefault();
            if (created != null)
            {
                var choreEvents = await store.FetchEventsAsync($"chore-{created.HouseholdId}");
                history.AddRange(choreEvents.Cast<object>());
            }

            households.Add(history);
        }

        return PlatformStatsView.Project(households, dataPath, Directory.Exists(dataPath), DateTime.UtcNow);
    }
}

internal static class PlatformStatsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("stats", async (Handler handler) =>
        {
            var result = await handler.HandleAsync();
            return Results.Ok(result);
        })
        .WithName("GetPlatformStats");
    }
}
