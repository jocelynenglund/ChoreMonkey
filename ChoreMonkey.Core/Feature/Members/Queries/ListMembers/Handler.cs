using ChoreMonkey.Core.Domain;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Members.Queries.ListMembers;

public record ListMembersQuery(Guid HouseholdId);
public record MemberResponseDto(Guid MemberId, string Nickname, string? Status = null);
public record ListMembersResponse(List<MemberResponseDto> Members);

internal class Handler(IEventStore store)
{
    public async Task<ListMembersResponse> HandleAsync(ListMembersQuery request)
    {
        var history = (await store.FetchEventsAsync(
            HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>().ToList();

        return ListMembersView.Project(history);
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
