using FileEventStore;
using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Activity.Queries.CompletionTimeline;

public record GetCompletionTimelineQuery(Guid HouseholdId);

public record ActivityEntry(
    string Type,           // "completion", "assignment", "nickname_change", "status_change", "member_joined", "member_removed", "chore_created"
    string Description,    // Human-readable description
    DateTimeOffset Timestamp,
    Guid? ChoreId = null,
    Guid? MemberId = null
);

public record GetCompletionTimelineResponse(List<ActivityEntry> Activities);

internal class Handler(IEventStore store)
{
    public async Task<GetCompletionTimelineResponse> HandleAsync(GetCompletionTimelineQuery request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>()
            .Concat((await store.FetchEventsAsync(choreStreamId)).Cast<object>())
            .ToList();
        return CompletionTimelineView.Project(history, DateTime.UtcNow);
    }
}

public static class CompletionTimelineEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{householdId}/activity", async (
            Guid householdId,
            Handler handler) =>
        {
            var response = await handler.HandleAsync(
                new GetCompletionTimelineQuery(householdId));
            return Results.Ok(response);
        });
    }
}
