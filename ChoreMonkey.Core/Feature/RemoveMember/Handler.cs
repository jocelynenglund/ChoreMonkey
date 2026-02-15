using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.RemoveMember;

public record RemoveMemberCommand(
    Guid HouseholdId,
    Guid MemberId,
    Guid RemovedByMemberId,
    string PinCode);

public record RemoveMemberRequest(Guid RemovedByMemberId);

internal class Handler(IEventStore store)
{
    public async Task<(bool Success, string? Error)> HandleAsync(RemoveMemberCommand command)
    {
        var streamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        // Verify household exists
        var householdCreated = events.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
            return (false, "Household not found");
        
        // Verify admin PIN
        var adminPinEvent = events.OfType<AdminPinChanged>().LastOrDefault();
        var adminPinHash = adminPinEvent?.NewPinHash ?? householdCreated.PinHash;
        
        if (!int.TryParse(command.PinCode, out var pinInt) || !PinHasher.VerifyPin(pinInt, adminPinHash))
            return (false, "Invalid admin PIN");
        
        // Find the member to remove
        var memberJoined = events.OfType<MemberJoinedHousehold>()
            .FirstOrDefault(m => m.MemberId == command.MemberId);
        
        if (memberJoined == null)
            return (false, "Member not found");
        
        // Check if member was already removed
        var alreadyRemoved = events.OfType<MemberRemoved>()
            .Any(m => m.MemberId == command.MemberId);
        
        if (alreadyRemoved)
            return (false, "Member already removed");
        
        // Can't remove yourself
        if (command.MemberId == command.RemovedByMemberId)
            return (false, "Cannot remove yourself");
        
        // Get the nickname (might have been changed)
        var nicknameChange = events.OfType<MemberNicknameChanged>()
            .Where(n => n.MemberId == command.MemberId)
            .LastOrDefault();
        var nickname = nicknameChange?.NewNickname ?? memberJoined.Nickname;
        
        var removedEvent = new MemberRemoved(
            command.HouseholdId,
            command.MemberId,
            command.RemovedByMemberId,
            nickname);
        
        await store.AppendToStreamAsync(streamId, removedEvent, ExpectedVersion.Any);
        
        return (true, null);
    }
}

internal static class RemoveMemberEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/members/{memberId:guid}/remove", async (
            Guid householdId,
            Guid memberId,
            RemoveMemberRequest request,
            HttpContext httpContext,
            Handler handler) =>
        {
            var pinCode = httpContext.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCode))
                return Results.Unauthorized();
            
            var command = new RemoveMemberCommand(
                householdId,
                memberId,
                request.RemovedByMemberId,
                pinCode);
            
            var (success, error) = await handler.HandleAsync(command);
            
            if (!success)
            {
                return error switch
                {
                    "Invalid admin PIN" => Results.Json(new { error }, statusCode: StatusCodes.Status403Forbidden),
                    "Household not found" => Results.NotFound(),
                    "Member not found" => Results.NotFound(),
                    _ => Results.BadRequest(new { error })
                };
            }
            
            return Results.Ok();
        });
    }
}
