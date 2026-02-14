using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.ListMembers;

public record ListMembersQuery(Guid HouseholdId);
public record MemberDto(Guid MemberId, string Nickname);
public record ListMembersResponse(List<MemberDto> Members);

internal class Handler(IEventStore store)
{
    public async Task<ListMembersResponse> HandleAsync(ListMembersQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        var members = events.OfType<MemberJoinedHousehold>()
            .Select(e => new MemberDto(e.MemberId, e.Nickname))
            .ToList();
        
        return new ListMembersResponse(members);
    }
}

internal static class ListMembersEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/members", async (
            Guid householdId,
            Handler handler) =>
        {
            var query = new ListMembersQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
