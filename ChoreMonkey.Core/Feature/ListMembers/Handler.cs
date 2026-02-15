using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.ListMembers;

public record ListMembersQuery(Guid HouseholdId);
public record MemberDto(Guid MemberId, string Nickname, string? Status = null);
public record ListMembersResponse(List<MemberDto> Members);

internal class Handler(IEventStore store)
{
    public async Task<ListMembersResponse> HandleAsync(ListMembersQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        // Get all members who joined
        var joinedMembers = events.OfType<MemberJoinedHousehold>().ToList();
        
        // Get removed member IDs
        var removedMemberIds = events.OfType<MemberRemoved>()
            .Select(e => e.MemberId)
            .ToHashSet();
        
        // Get latest nickname changes per member
        var nicknameChanges = events.OfType<MemberNicknameChanged>()
            .GroupBy(e => e.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.TimestampUtc).First().NewNickname
            );
        
        // Get latest status per member
        var statusChanges = events.OfType<MemberStatusChanged>()
            .GroupBy(e => e.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.TimestampUtc).First().Status
            );
        
        var members = joinedMembers
            .Where(e => !removedMemberIds.Contains(e.MemberId))
            .Select(e => new MemberDto(
                e.MemberId, 
                nicknameChanges.GetValueOrDefault(e.MemberId, e.Nickname),
                statusChanges.GetValueOrDefault(e.MemberId)
            ))
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
