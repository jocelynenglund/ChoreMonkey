using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.ChoreHistory;

public record GetChoreHistoryQuery(Guid HouseholdId, Guid ChoreId);

public record CompletionDto(Guid CompletedBy, DateTime CompletedAt);

public record ChoreHistoryResponse(List<CompletionDto> Completions);

internal class Handler(IEventStore store)
{
    public async Task<ChoreHistoryResponse> HandleAsync(GetChoreHistoryQuery request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);

        var completions = events.OfType<ChoreCompleted>()
            .Where(e => e.ChoreId == request.ChoreId)
            .OrderByDescending(e => e.CompletedAt)
            .Select(e => new CompletionDto(e.CompletedByMemberId, e.CompletedAt))
            .ToList();

        return new ChoreHistoryResponse(completions);
    }
}

internal static class ChoreHistoryEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/chores/{choreId:guid}/history", async (
            Guid householdId,
            Guid choreId,
            Handler handler) =>
        {
            var query = new GetChoreHistoryQuery(householdId, choreId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
