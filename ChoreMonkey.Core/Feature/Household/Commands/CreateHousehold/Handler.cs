using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.Household.Commands.CreateHousehold;

public record CreateHouseholdCommand(Guid HouseholdId, string Name, int PinCode, string OwnerNickname = "Admin", int? MemberPinCode = null);
public record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);

internal class Handler(IEventStore store)
{
    public async Task<CreateHouseholdResponse> HandleAsync(CreateHouseholdCommand request)
    {
        // Hashing is randomized and slow, so it happens here and the hashes go into Decide.
        var adminPinHash = PinHasher.HashPin(request.PinCode);
        var memberPinHash = request.MemberPinCode.HasValue
            ? PinHasher.HashPin(request.MemberPinCode.Value)
            : null;
        var memberId = Guid.NewGuid();

        var decision = CreateHouseholdSlice.Decide(new List<object>(), request, adminPinHash, memberPinHash, memberId);

        await store.StartStreamAsync(
            HouseholdAggregate.StreamId(request.HouseholdId),
            decision.Events.Cast<IStoreableEvent>().ToArray());

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
                request.OwnerNickname ?? "Admin",
                request.MemberPinCode
            );
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}

public record CreateHouseholdRequest(string Name, int PinCode, Guid? HouseholdId = null, string? OwnerNickname = null, int? MemberPinCode = null);
