using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.CreateHousehold;

public record CreateHouseholdCommand(Guid HouseholdId, string Name, int PinCode, string OwnerNickname = "Admin");
public record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);

internal class Handler(IEventStore store)
{
    public async Task<CreateHouseholdResponse> HandleAsync(CreateHouseholdCommand request)
    {
        var pinHash = PinHasher.HashPin(request.PinCode);
        var memberId = Guid.NewGuid();
        
        // Create household and add owner as first member
        var householdCreated = new HouseholdCreated(request.HouseholdId, request.Name, pinHash);
        var memberJoined = new MemberJoinedHousehold(memberId, request.HouseholdId, Guid.Empty, request.OwnerNickname);
        
        await store.StartStreamAsync(
            HouseholdAggregate.StreamId(request.HouseholdId), 
            new IStoreableEvent[] { householdCreated, memberJoined });
        
        return new CreateHouseholdResponse(request.HouseholdId, memberId, request.Name);
    }
}

internal static class CreateHouseholdEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households", async (CreateHouseholdRequest request, Handler handler) =>
        {
            var command = new CreateHouseholdCommand(
                request.HouseholdId ?? Guid.NewGuid(),
                request.Name,
                request.PinCode,
                request.OwnerNickname ?? "Admin"
            );
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}

public record CreateHouseholdRequest(string Name, int PinCode, Guid? HouseholdId = null, string? OwnerNickname = null);
