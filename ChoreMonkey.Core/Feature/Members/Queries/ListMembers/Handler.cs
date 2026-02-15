using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Members.Queries.ListMembers;

public record ListMembersQuery(Guid HouseholdId);
public record MemberResponseDto(Guid MemberId, string Nickname, string? Status = null);
public record ListMembersResponse(List<MemberResponseDto> Members);

internal class Handler(ISender mediator)
{
    public async Task<ListMembersResponse> HandleAsync(ListMembersQuery request)
    {
        var lookup = await mediator.Send(new MemberLookupQuery(request.HouseholdId));
        
        var members = lookup.Members.Values
            .Select(m => new MemberResponseDto(m.Id, m.Nickname, m.Status))
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
