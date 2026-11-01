using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Victories;

public record GetVictoriesQuery(Guid HouseholdId, int Limit = 10);

public record VictoryDto(
    Guid ChoreId,
    Guid MemberId,
    string MemberNickname,
    string ChoreName,
    DateTime CompletedAt);

public record GetVictoriesResponse(List<VictoryDto> Victories);

internal class Handler(IEventStore store)
{
    public async Task<GetVictoriesResponse> HandleAsync(GetVictoriesQuery request)
    {
        var householdEvents = await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId));
        var choreEvents     = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var history = householdEvents.Cast<object>().Concat(choreEvents.Cast<object>()).ToList();
        return VictoriesView.Project(history, request.Limit);
    }
}

internal static class VictoriesEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/victories",
            async (Guid householdId, Handler handler, int limit = 10) =>
            {
                var result = await handler.HandleAsync(new GetVictoriesQuery(householdId, limit));
                return Results.Ok(result);
            });
    }
}
