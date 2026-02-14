using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace ChoreMonkey.Core.Feature.InviteLink;

public record GetInviteQuery(Guid HouseholdId);
public record InviteLinkResponse(Guid HouseholdId, Guid InviteId, string Link);

internal class Handler(IEventStore store)
{
    public async Task<InviteLinkResponse?> HandleAsync(GetInviteQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);

        var invite = events.OfType<InviteGenerated>().LastOrDefault();
        if (invite == null) return null;

        return new InviteLinkResponse(invite.HouseholdId, invite.InviteId, invite.Link);
    }
}

internal static class InviteLinkEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/invite", async (Guid householdId, Feature.InviteLink.Handler handler) =>
        {
            var query = new GetInviteQuery(householdId);
            var result = await handler.HandleAsync(query);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
