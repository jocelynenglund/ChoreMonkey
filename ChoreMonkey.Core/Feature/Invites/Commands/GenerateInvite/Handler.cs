using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.Invites.Commands.GenerateInvite;

public record GenerateInviteCommand(Guid HouseholdId);
public record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);

internal class Handler(IEventStore store)
{
    public async Task<GenerateInviteResponse> HandleAsync(GenerateInviteCommand request)
    {
        var inviteId = Guid.NewGuid();
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);

        var decision = GenerateInviteSlice.Decide(new List<object>(), request, inviteId);

        var invite = decision.Events.OfType<InviteGenerated>().Single();
        await store.AppendToStreamAsync(streamId, invite, ExpectedVersion.Any);

        return new GenerateInviteResponse(request.HouseholdId, inviteId, invite.Link);
    }
}

internal static class GenerateInviteEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/invite", async (Guid householdId, Feature.Invites.Commands.GenerateInvite.Handler handler) =>
        {
            var command = new GenerateInviteCommand(householdId);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
