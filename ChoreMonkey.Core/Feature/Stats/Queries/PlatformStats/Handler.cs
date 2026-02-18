using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    List<HouseholdStatsDto> Households);

internal class Handler(IEventStore store)
{
    private static readonly string DataPath = Environment.GetEnvironmentVariable("EVENTSTORE_PATH") 
        ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
    
    public async Task<PlatformStatsResponse> HandleAsync()
    {
        // Scan data directory for household stream files
        var householdStreamIds = new List<string>();
        
        if (Directory.Exists(DataPath))
        {
            var files = Directory.GetFiles(DataPath, "household-*.jsonl");
            householdStreamIds = files
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();
        }
        
        var households = new List<HouseholdStatsDto>();
        var totalMembers = 0;
        var totalChores = 0;
        
        foreach (var streamId in householdStreamIds)
        {
            var events = await store.FetchEventsAsync(streamId);
            
            // Get household created event
            var created = events.OfType<HouseholdCreated>().FirstOrDefault();
            if (created == null) continue;
            
            // Count members (joined - removed)
            var memberJoins = events.OfType<MemberJoinedHousehold>()
                .Select(e => e.MemberId)
                .ToHashSet();
            var memberRemovals = events.OfType<MemberRemoved>()
                .Select(e => e.MemberId)
                .ToHashSet();
            var memberCount = memberJoins.Except(memberRemovals).Count();
            
            // Get chore count from chore stream
            var choreStreamId = $"chore-{created.HouseholdId}";
            var choreEvents = await store.FetchEventsAsync(choreStreamId);
            
            var choreCreations = choreEvents.OfType<ChoreCreated>()
                .Select(e => e.ChoreId)
                .ToHashSet();
            var choreDeletions = choreEvents.OfType<ChoreDeleted>()
                .Select(e => e.ChoreId)
                .ToHashSet();
            var choreCount = choreCreations.Except(choreDeletions).Count();
            
            // Parse created timestamp
            var createdAt = DateTime.TryParse(created.TimestampUtc, out var parsed) 
                ? parsed 
                : DateTime.UtcNow;
            
            households.Add(new HouseholdStatsDto(
                created.HouseholdId,
                created.Name,
                memberCount,
                choreCount,
                createdAt));
            
            totalMembers += memberCount;
            totalChores += choreCount;
        }
        
        return new PlatformStatsResponse(
            households.Count,
            totalMembers,
            totalChores,
            households.OrderByDescending(h => h.CreatedAt).ToList());
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
