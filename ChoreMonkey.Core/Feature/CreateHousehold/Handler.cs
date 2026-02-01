using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.CreateHousehold;


public record CreateHouseholdCommand(Guid HouseholdId, string Name, int PinCode);

internal class Handler(IEventStore store)
{

    public async Task HandleAsync(CreateHouseholdCommand request)
    {
        var pinHash = PinHasher.HashPin(request.PinCode);
        await store.AppendAsync(HouseholdAggregate.StreamId(request.HouseholdId), new HouseholdCreated(request.HouseholdId, request.Name, pinHash), ExpectedVersion.None);
    }
}
internal static class CreateHouseholdEndpoint
{

    public static void Map(this RouteGroupBuilder group)
    {

        group.MapPost("households", async (CreateHouseholdCommand command, Feature.CreateHousehold.Handler handler) =>
        {
            await handler.HandleAsync(command);
            return Results.Ok();
        });

    }
}
