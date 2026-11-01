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
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();
        return ChoreHistoryView.Project(history, request.ChoreId);
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
