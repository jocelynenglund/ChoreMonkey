using FileEventStore;
using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.CompletionTimeline;

public record GetCompletionTimelineQuery(Guid HouseholdId, int? Limit = 50, int? Days = 7);

public record CompletionEntry(
    Guid ChoreId,
    string ChoreName,
    Guid CompletedBy,
    string CompletedByNickname,
    DateTimeOffset CompletedAt
);

public record GetCompletionTimelineResponse(List<CompletionEntry> Completions);

internal class Handler(IEventStore store)
{
    public async Task<GetCompletionTimelineResponse> HandleAsync(GetCompletionTimelineQuery request)
    {
        var maxDays = request.Days ?? 7;
        var maxItems = request.Limit ?? 50;
        var cutoff = DateTime.UtcNow.AddDays(-maxDays);

        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Get all chore names
        var choreNames = choreEvents
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        // Get all member nicknames
        var memberNicknames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(m => m.MemberId, m => m.Nickname);

        // Get all completions within time range, sorted by most recent
        var completions = choreEvents
            .OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt >= cutoff)
            .OrderByDescending(c => c.CompletedAt)
            .Take(maxItems)
            .Select(c => new CompletionEntry(
                c.ChoreId,
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown"),
                c.CompletedByMemberId,
                memberNicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown"),
                c.CompletedAt
            ))
            .ToList();

        return new GetCompletionTimelineResponse(completions);
    }
}

public static class CompletionTimelineEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{householdId}/completions", async (
            Guid householdId,
            Handler handler,
            int? limit,
            int? days) =>
        {
            var response = await handler.HandleAsync(
                new GetCompletionTimelineQuery(householdId, limit, days));
            return Results.Ok(response);
        });
    }
}
