using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.GenerateInvite;

public record GenerateInviteCommand(Guid HouseholdId);
public record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);

internal class Handler(IEventStore store)
{
    public async Task<GenerateInviteResponse> HandleAsync(GenerateInviteCommand request)
    {
        var inviteId = Guid.NewGuid();
        var link = $"https://invite.example/{inviteId}";

        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        await store.AppendAsync(streamId, new InviteGenerated(request.HouseholdId, inviteId, link), ExpectedVersion.Any);

        return new GenerateInviteResponse(request.HouseholdId, inviteId, link);
    }
}

internal static class GenerateInviteEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/invite", async (Guid householdId, Feature.GenerateInvite.Handler handler) =>
        {
            var command = new GenerateInviteCommand(householdId);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
